using RimWorld;
using Verse;
using UnityEngine;
using System.Text;

namespace EmergencyExpanded
{
    public class HediffComp_Contamination : HediffComp
    {
        public HediffCompProperties_Contamination Props => (HediffCompProperties_Contamination)this.props;

        // 污染度 0.0 ~ 1.0+
        public float contamination = 0f;
        
        // 初始评估标记，防止重复赋予初始污染
        private bool initialized = false;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref contamination, "contamination", 0f);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (!initialized && dinfo.HasValue)
            {
                InitializeContamination(dinfo.Value);
                initialized = true;
            }
        }

        private void InitializeContamination(DamageInfo dinfo)
        {
            float baseContamination = 0.05f; // 默认基础污染

            if (dinfo.Def != null)
            {
                // 枪伤/破片伤：初始污染高 (兼容 CE 远程弹药与破片)
                if (dinfo.Def.isRanged || dinfo.Def.defName.Contains("Fragment"))
                {
                    baseContamination += 0.15f;
                }
                // 动物撕咬：污染极高
                else if (dinfo.Def == DamageDefOf.Bite || dinfo.Def.defName.IndexOf("bite", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    baseContamination += 0.25f;
                }
                // 利器砍伤 (兼容各类锋利近战武器)
                else if (dinfo.Def.armorCategory == DamageArmorCategoryDefOf.Sharp)
                {
                    baseContamination += 0.10f;
                }
                // 钝器伤/开放性骨折
                else if (dinfo.Def == DamageDefOf.Blunt || dinfo.Def == DamageDefOf.Crush || (dinfo.Def.armorCategory != null && dinfo.Def.armorCategory.defName == "Blunt"))
                {
                    baseContamination += 0.05f;
                }
            }

            this.contamination = Mathf.Clamp01(baseContamination);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Dead) return;

            // 每 60 tick (1秒) 检查一次环境，频率适中，不会漏掉短暂的倒地
            if (Pawn.IsHashIntervalTick(60))
            {
                UpdateContaminationFromEnvironment();
                CheckInfectionProgression();
            }
        }

        private void UpdateContaminationFromEnvironment()
        {
            // 如果伤口已经被治愈（清创），污染度不再增加
            if (this.contamination <= 0f) return;

            float environmentFactor = 0f;

            // 倒地状态，直接接触地面
            if (Pawn.Downed && Pawn.Spawned)
            {
                TerrainDef terrain = Pawn.Position.GetTerrain(Pawn.Map);
                if (terrain != null)
                {
                    // 肮脏的地形（泥地、沼泽等）
                    if (terrain.defName.Contains("Mud") || terrain.defName.Contains("Water") || terrain.defName.Contains("Swamp"))
                    {
                        environmentFactor += 0.0005f; // 每秒增加 0.05%，一天(1000秒)增加 50%
                    }
                    // 地板清洁度影响
                    else
                    {
                        float cleanliness = terrain.GetStatValueAbstract(StatDefOf.Cleanliness);
                        if (cleanliness < 0)
                        {
                            environmentFactor += 0.0002f * Mathf.Abs(cleanliness);
                        }
                    }
                }

                // 地面上是否有血迹、呕吐物等污垢
                var thingList = Pawn.Position.GetThingList(Pawn.Map);
                bool hasFilth = false;
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (thingList[i].def.category == ThingCategory.Filth)
                    {
                        hasFilth = true;
                        break;
                    }
                }

                if (hasFilth)
                {
                    environmentFactor += 0.0003f;
                }
            }

            // 伤口未包扎时，自然污染微量上升
            if (!this.parent.IsTended())
            {
                environmentFactor += 0.0001f; // 降低自然恶化速度
            }

            if (environmentFactor > 0f)
            {
                this.contamination = Mathf.Clamp01(this.contamination + environmentFactor);
            }
        }

        private void CheckInfectionProgression()
        {
            if (this.contamination >= 0.35f)
            {
                // 判断应该引发哪种局部感染
                bool isBlunt = this.parent.def.defName.Contains("Bruise") || this.parent.def.defName.Contains("Crush") || this.parent.def.defName.Contains("Blunt");
                HediffDef targetLocalInfection = isBlunt ? EE_DefOf.EE_Necrosis : EE_DefOf.EE_LocalizedInfection;

                // 检查该部位是否已经有该感染
                if (!Pawn.health.hediffSet.HasHediff(targetLocalInfection, this.parent.Part))
                {
                    Pawn.health.AddHediff(targetLocalInfection, this.parent.Part);
                    if (Pawn.Spawned)
                    {
                        Messages.Message($"{Pawn.LabelShort}的伤口由于污染过度，引发了{targetLocalInfection.label}！", Pawn, MessageTypeDefOf.NegativeHealthEvent);
                    }
                }
            }

            if (this.contamination >= 0.85f)
            {
                // 触发全身败血症
                if (!Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Sepsis))
                {
                    Pawn.health.AddHediff(EE_DefOf.EE_Sepsis);
                    if (Pawn.Spawned)
                    {
                        Find.LetterStack.ReceiveLetter("败血症", $"{Pawn.LabelShort}的伤口感染极度恶化，细菌入血引发了败血症！如果不及时治疗将引发多器官衰竭并危及生命！", LetterDefOf.ThreatSmall, Pawn);
                    }
                    
                    // 联动触发 SIRS
                    if (EE_DefOf.SIRS != null && !Pawn.health.hediffSet.HasHediff(EE_DefOf.SIRS))
                    {
                        Pawn.health.AddHediff(EE_DefOf.SIRS);
                    }
                }
            }
        }

        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            base.CompTended(quality, maxQuality, batchPosition);

            // 包扎可以降低污染度，品质越高降低越多
            float reduction = 0.05f + (quality * 0.15f); 
            this.contamination = Mathf.Clamp01(this.contamination - reduction);
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (contamination > 0.10f)
                {
                    return "污染度: " + (contamination * 100f).ToString("F0") + "%";
                }
                return null;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                if (contamination > 0.0f)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("伤口污染度: " + (contamination * 100f).ToString("F1") + "%");
                    if (contamination > 0.5f)
                    {
                        sb.AppendLine("警告：高污染度，极易引发严重感染，建议立即进行清创！");
                    }
                    else if (contamination > 0.2f)
                    {
                        sb.AppendLine("伤口有感染风险。");
                    }
                    return sb.ToString().TrimEndNewlines();
                }
                return null;
            }
        }
    }
}
