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
            this.defaultLabel = "宣布死亡";
            this.defaultDesc = "放弃对该伤员的所有抢救尝试，正式宣布其死亡。";
            
            // 使用原版的骷髅头图标或者医疗相关图标
            this.icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoCare", true); 
            if (this.icon == null) 
            {
                this.icon = TexCommand.Attack; // 备用图标
            }
            
            this.action = delegate ()
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    $"你确定要对 {patient.NameShortColored} 宣布死亡吗？这将会直接终结该小人的生命。",
                    "确认",
                    delegate ()
                    {
                        ExecuteDeath();
                    },
                    "取消"
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
