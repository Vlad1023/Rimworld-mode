using Verse;
using Verse.AI;

namespace Rimworld
{
    public class JobDriver_EquipWeapon : JobDriver_Equip
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            int maxPawns = 1;
            int stackCount = -1;
            if (this.job.targetA.HasThing && this.job.targetA.Thing.Spawned && this.job.targetA.Thing.def.IsIngestible)
            {
                maxPawns = 1;
                stackCount = 1;
            }
            var reserved = this.pawn.Reserve(this.job.targetA, this.job, maxPawns, stackCount, errorOnFailed: errorOnFailed);
            return reserved;
        }
    }
}