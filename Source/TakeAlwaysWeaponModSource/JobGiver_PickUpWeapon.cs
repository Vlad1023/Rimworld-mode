using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Rimworld
{
    public class JobGiver_PickUpWeapon : ThinkNode_JobGiver
    {
      
      private bool preferBuildingDestroyers;
      private bool pickUpUtilityItems;
      
      private static Dictionary<Pawn, int> lastJobTick = new Dictionary<Pawn, int>();

      private float MinMeleeWeaponDPSThreshold
      {
        get
        {
          List<Tool> tools = ThingDefOf.Human.tools;
          float num = 0.0f;
          for (int index = 0; index < tools.Count; ++index)
          {
            if (tools[index].linkedBodyPartsGroup == BodyPartGroupDefOf.LeftHand || tools[index].linkedBodyPartsGroup == BodyPartGroupDefOf.RightHand)
            {
              num = tools[index].power / tools[index].cooldownTime;
              break;
            }
          }
          return num + 2f;
        }
      }

      public override ThinkNode DeepCopy(bool resolve = true)
      {
        JobGiver_PickUpWeapon opportunisticWeapon = (JobGiver_PickUpWeapon) base.DeepCopy(resolve);
        opportunisticWeapon.preferBuildingDestroyers = this.preferBuildingDestroyers;
        opportunisticWeapon.pickUpUtilityItems = this.pickUpUtilityItems;
        return (ThinkNode) opportunisticWeapon;
      }
      
      protected override Job TryGiveJob(Pawn pawn)
      {
        int currentTick = Find.TickManager.TicksGame;
        if (lastJobTick.ContainsKey(pawn) && currentTick - lastJobTick[pawn] < 100)
        {
          return null; // Too soon to assign any of the followed jobs again
        }
        if ((pawn.equipment == null && pawn.apparel == null) || pawn.CurJob?.def == JobDefOfEquipWeapon.EquipWeapon)
          return (Job) null;
        if (pawn.RaceProps.Humanlike && pawn.WorkTagIsDisabled(WorkTags.Violent))
          return (Job) null;
        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
          return (Job) null;
        if (pawn.GetRegion() == null)
          return (Job) null;
        if (pawn.equipment != null && !this.AlreadySatisfiedWithCurrentWeapon(pawn))
        {
          Thing targetA = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Weapon), PathEndMode.Touch, TraverseParms.For(pawn), 500f, (Predicate<Thing>) (x => pawn.CanReserve((LocalTargetInfo) x) && !x.IsBurning() && this.ShouldEquipWeapon(x, pawn)));
          if (targetA != null)
          {
            pawn.playerSettings.hostilityResponse = HostilityResponseMode.Attack; // so pawn is able to attack but it just a question of finding weapon.
            var job = JobMaker.MakeJob(JobDefOfEquipWeapon.EquipWeapon, (LocalTargetInfo)targetA);
            if (job != null)
            {
              lastJobTick[pawn] = currentTick; // Record when the job was assigned
            }
            return job;
          }
          if (pawn.equipment.Primary != null && !SlaveRebellionUtility.WeaponUsableInRebellion((Thing) pawn.equipment.Primary)) // would leave it here but I suppose might be removed
            return JobMaker.MakeJob(JobDefOf.DropEquipment, (LocalTargetInfo) (Thing) pawn.equipment.Primary);
        }
        Pawn_EquipmentTracker equipment = pawn.equipment;
        int num;
        if (equipment == null)
        {
          num = 0;
        }
        else
        {
          bool? isRangedWeapon = equipment.Primary?.def?.IsRangedWeapon;
          bool flag = true;
          num = isRangedWeapon.GetValueOrDefault() == flag & isRangedWeapon.HasValue ? 1 : 0;
        }
        if (num != 0)
        {
          foreach (Apparel targetA in pawn.apparel.WornApparel)
          {
            if (targetA.def == ThingDefOf.Apparel_ShieldBelt)
            {
              return JobMaker.MakeJob(JobDefOf.RemoveApparel, (LocalTargetInfo) (Thing) targetA);
            }
          }
        }
        if (this.pickUpUtilityItems && pawn.apparel != null && this.WouldPickupUtilityItem(pawn))
        {
          Thing targetA = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Apparel), PathEndMode.OnCell, TraverseParms.For(pawn), 8f, (Predicate<Thing>) (x => pawn.CanReserve((LocalTargetInfo) x) && !x.IsBurning() && this.ShouldEquipUtilityItem(x, pawn)), searchRegionsMax: 15);
          if (targetA != null)
            return JobMaker.MakeJob(JobDefOf.Wear, (LocalTargetInfo) targetA);
        }
        return (Job) null;
      }
      
      private bool AlreadySatisfiedWithCurrentWeapon(Pawn pawn)
      {
        ThingWithComps primary = pawn.equipment.Primary;
        if (primary == null)
          return false;
        if (this.preferBuildingDestroyers)
        {
          if (!pawn.equipment.PrimaryEq.PrimaryVerb.verbProps.ai_IsBuildingDestroyer)
            return false;
        }
        else if (!SlaveRebellionUtility.WeaponUsableInRebellion((Thing) primary)) // would leave it here but I suppose might be removed
          return false;
        return true;
      }

      private bool ShouldEquipWeapon(Thing newWep, Pawn pawn)
      {
        return (!newWep.def.IsRangedWeapon || !pawn.WorkTagIsDisabled(WorkTags.Shooting)) && EquipmentUtility.CanEquip(newWep, pawn) && this.GetWeaponScore(newWep) > this.GetWeaponScore((Thing) pawn.equipment.Primary) && SlaveRebellionUtility.WeaponUsableInRebellion(newWep);
      }

      private int GetWeaponScore(Thing wep)
      {
        if (wep == null || wep.def.IsMeleeWeapon && (double) wep.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS) < (double) this.MinMeleeWeaponDPSThreshold)
          return 0;
        if (this.preferBuildingDestroyers && wep.TryGetComp<CompEquippable>().PrimaryVerb.verbProps.ai_IsBuildingDestroyer)
          return 3;
        return wep.def.IsRangedWeapon ? 2 : 1;
      }

      private bool WouldPickupUtilityItem(Pawn pawn)
      {
        return pawn.equipment?.Primary == null && pawn.apparel.FirstApparelVerb == null;
      }

      private bool ShouldEquipUtilityItem(Thing thing, Pawn pawn)
      {
        return thing is Apparel newApparel && newApparel.def.apparel.ai_pickUpOpportunistically && EquipmentUtility.CanEquip((Thing) newApparel, pawn) && ApparelUtility.HasPartsToWear(pawn, newApparel.def) && !pawn.apparel.WouldReplaceLockedApparel(newApparel);
      }
      
    }
}