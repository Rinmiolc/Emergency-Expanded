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
            return pawn.Reserve(Patient, job, 5, 0, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PatientIndex);
            this.FailOn(() => Patient.Dead);

            bool wasDesignated = EE_DefOf.EE_FastFirstAid != null && Map.designationManager.DesignationOn(Patient, EE_DefOf.EE_FastFirstAid) != null;
            if (wasDesignated)
            {
                this.FailOn(() => Map.designationManager.DesignationOn(Patient, EE_DefOf.EE_FastFirstAid) == null);
                
                this.AddFinishAction((JobCondition condition) => 
                {
                    if (EE_DefOf.EE_FastFirstAid != null && Map.designationManager.DesignationOn(Patient, EE_DefOf.EE_FastFirstAid) != null)
                    {
                        bool anyoneElseTreating = false;
                        foreach (Pawn p in Map.mapPawns.FreeColonistsSpawned)
                        {
                            if (p != pawn && p.CurJob != null && p.CurJob.def == EE_DefOf.EE_ApplyFirstAid && p.CurJob.GetTarget(TargetIndex.A).Thing == Patient)
                            {
                                anyoneElseTreating = true;
                                break;
                            }
                            if (p != pawn && p.jobs != null && p.jobs.jobQueue != null)
                            {
                                foreach (var qj in p.jobs.jobQueue)
                                {
                                    if (qj.job.def == EE_DefOf.EE_ApplyFirstAid && qj.job.GetTarget(TargetIndex.A).Thing == Patient)
                                    {
                                        anyoneElseTreating = true;
                                        break;
                                    }
                                }
                            }
                            if (anyoneElseTreating) break;
                        }
                        
                        if (!anyoneElseTreating)
                        {
                            Map.designationManager.DesignationOn(Patient, EE_DefOf.EE_FastFirstAid)?.Delete();
                        }
                    }
                });
            }

            yield return Toils_Goto.GotoThing(PatientIndex, PathEndMode.Touch);

            Toil checkCondition = new Toil();
            Toil extractMedicine = new Toil();
            Toil treatToil = ToilMaker.MakeToil("ApplyFirstAid");

            checkCondition.initAction = () =>
            {
                ThingDef medDef = job.GetTarget(MedicineIndex).Thing?.def;
                if (medDef == null)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                EmergencyItemType type = EE_FirstAidUtility.GetEmergencyItemType(medDef);
                if (!EE_FirstAidUtility.CanApplyToTarget(Patient, type, medDef))
                {
                    this.EndJobWith(JobCondition.Succeeded);
                    return;
                }

                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried == null || carried.def != medDef)
                {
                    bool hasMedInInv = false;
                    foreach (Thing t in pawn.inventory.innerContainer)
                    {
                        if (t.def == medDef)
                        {
                            hasMedInInv = true;
                            break;
                        }
                    }
                    if (!hasMedInInv)
                    {
                        this.EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    this.JumpToToil(extractMedicine);
                }
            };

            extractMedicine.initAction = () =>
            {
                ThingDef medDef = job.GetTarget(MedicineIndex).Thing?.def;
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried != null && carried.def == medDef) return;

                Thing invMed = null;
                foreach (Thing t in pawn.inventory.innerContainer)
                {
                    if (t.def == medDef)
                    {
                        invMed = t;
                        break;
                    }
                }

                if (invMed != null)
                {
                    pawn.inventory.innerContainer.TryTransferToContainer(invMed, pawn.carryTracker.innerContainer, 1, out Thing carriedThing);
                    job.SetTarget(MedicineIndex, carriedThing);
                }
            };

            treatToil.defaultCompleteMode = ToilCompleteMode.Never;
            treatToil.initAction = () =>
            {
                ThingDef medDef = job.GetTarget(MedicineIndex).Thing?.def;
                int duration = CalculateTreatmentTicks(medDef, Patient);
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
                
                // 确保在整个包扎/施药读条期间，被施救小人维持站立且不得移动走开
                if (Patient.Spawned && !Patient.Downed && Patient.CurrentBed() == null)
                {
                    if (Patient.stances != null && Patient.stances.stunner != null && Patient.stances.stunner.StunTicksLeft < 10)
                    {
                        Patient.pather?.StopDead();
                        Patient.stances.stunner.StunFor(60, pawn, false, false);
                    }
                    Patient.rotationTracker?.FaceCell(pawn.Position);
                }
                
                ticksLeftThisToil--;
                if (ticksLeftThisToil <= 0)
                {
                    EE_FirstAidUtility.ApplyFirstAidEffect(pawn, Patient, job.GetTarget(MedicineIndex).Thing);
                    this.JumpToToil(checkCondition);
                }
            };

            yield return checkCondition;
            yield return extractMedicine;
            yield return treatToil;
        }

        private int CalculateTreatmentTicks(ThingDef def, Pawn patient)
        {
            EmergencyItemType type = EE_FirstAidUtility.GetEmergencyItemType(def);
            
            if (type == EmergencyItemType.Tourniquet) return 180;   // 3.0 seconds
            
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
            
            bool inBed = patient.CurrentBed() != null;
            if (!inBed)
            {
                // Ground tend speed penalty: 2.5x slower (10.0 seconds)
                baseTicks = (int)(baseTicks * 2.5f); 
            }

            return baseTicks;
        }


    }
}
