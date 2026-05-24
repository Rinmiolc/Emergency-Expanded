using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace EmergencyExpanded
{
    public class JobDriver_ApplyFirstAid : JobDriver
    {
        private const TargetIndex PatientIndex = TargetIndex.A;
        private const TargetIndex MedicineIndex = TargetIndex.B;

        private Pawn Patient => (Pawn)job.GetTarget(PatientIndex).Thing;
        private Thing Medicine => job.GetTarget(MedicineIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Patient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PatientIndex);
            this.FailOn(() => Patient.Dead);
            this.FailOn(() => Medicine == null || !pawn.inventory.innerContainer.Contains(Medicine));

            yield return Toils_Goto.GotoThing(PatientIndex, PathEndMode.Touch);

            Toil treatToil = ToilMaker.MakeToil("ApplyFirstAid");
            treatToil.defaultCompleteMode = ToilCompleteMode.Never;
            
            treatToil.initAction = () =>
            {
                int duration = CalculateTreatmentTicks(Medicine.def, Patient);
                treatToil.defaultDuration = duration;
                ticksLeftThisToil = duration;
            };

            treatToil.PlaySustainerOrSound(SoundDef.Named("Recipe_Anesthetize"));
            
            treatToil.WithProgressBar(PatientIndex, () => {
                int totalDuration = treatToil.defaultDuration;
                if (totalDuration <= 0) return 0f;
                return (float)(totalDuration - ticksLeftThisToil) / totalDuration;
            });
            
            treatToil.tickAction = () =>
            {
                pawn.rotationTracker.FaceCell(Patient.Position);
                if (pawn.IsHashIntervalTick(30))
                {
                    // 播放基础微光或通过 Tend 音效表达
                }
                
                ticksLeftThisToil--;
                if (ticksLeftThisToil <= 0)
                {
                    EE_FirstAidUtility.ApplyFirstAidEffect(pawn, Patient, Medicine);
                    ReadyForNextToil();
                }
            };

            yield return treatToil;
        }

        private int CalculateTreatmentTicks(ThingDef def, Pawn patient)
        {
            EmergencyItemType type = EE_FirstAidUtility.GetEmergencyItemType(def);
            
            if (type == EmergencyItemType.Tourniquet) return 60;   // 1.0 seconds
            if (type == EmergencyItemType.AdrenalinePen) return 60; // 1.0 seconds
            
            if (type == EmergencyItemType.FirstAidKit)
            {
                return 90; // 1.5 seconds - no bed penalty
            }

            if (type == EmergencyItemType.IngestibleDirect)
            {
                return 100; // 1.6 seconds
            }

            // Normal medicine: 4.0 seconds base
            int baseTicks = 240;
            
            bool inMedicalBed = patient.CurrentBed() != null && patient.CurrentBed().Medical;
            if (!inMedicalBed)
            {
                // Ground tend speed penalty: 2.5x slower (10.0 seconds)
                baseTicks = (int)(baseTicks * 2.5f); 
            }

            return baseTicks;
        }
    }
}
