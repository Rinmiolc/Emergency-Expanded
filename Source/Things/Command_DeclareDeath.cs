using RimWorld;
using UnityEngine;
using Verse;

namespace EmergencyExpanded
{
    public class Command_DeclareDeath : Command_Action
    {
        private Pawn patient;

        public Command_DeclareDeath(Pawn pawn)
        {
            this.patient = pawn;
            this.defaultLabel = "EE_CommandDeclareDeath_Label".Translate();
            this.defaultDesc = "EE_CommandDeclareDeath_Desc".Translate();
            
            // 使用原版的骷髅头图标或者医疗相关图标
            this.icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoCare", true); 
            if (this.icon == null) 
            {
                this.icon = TexCommand.Attack; // 备用图标
            }
            
            this.action = delegate ()
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "EE_ConfirmDeclareDeathDesc".Translate(patient.NameShortColored),
                    "EE_Confirm".Translate(),
                    delegate ()
                    {
                        ExecuteDeath();
                    },
                    "EE_Cancel".Translate()
                ));
            };
        }

        private void ExecuteDeath()
        {
            if (patient == null || patient.Dead) return;

            // 添加宣布死亡状态
            Hediff deathCause = HediffMaker.MakeHediff(EE_DefOf.EE_DeclaredDead, patient, null);
            patient.health.AddHediff(deathCause, null, null, null);

            // 杀除小人
            patient.Kill(null, deathCause);
        }
    }
}
