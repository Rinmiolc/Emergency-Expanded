using System.Linq;
using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class HediffCompProperties_TxaEffect : HediffCompProperties
    {
        public HediffCompProperties_TxaEffect()
        {
            this.compClass = typeof(HediffComp_TxaEffect);
        }
    }

    public class HediffComp_TxaEffect : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead) return;
            if (!Pawn.IsHashIntervalTick(250)) return;

            float severity = parent.Severity;

            // ================= 1. 正面药效：消除/逆转凝血功能障碍 (Coagulopathy) =================
            Hediff coagulopathy = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.Coagulopathy);
            if (coagulopathy != null)
            {
                // 按照常量设定的每日速度扣减凝血障碍 (RareTick = 250 ticks)
                float reduction = (EE_Constants.TxaCoagulopathyReductionPerDay / 60000f) * 250f;
                coagulopathy.Severity = UnityEngine.Mathf.Max(coagulopathy.Severity - reduction, 0f);
            }

            // ================= 2. 负面毒理：过量高凝与脑毒性事件 =================
            if (severity > EE_Constants.OverdoseToxicityThreshold)
            {
                bool isFatal = severity > EE_Constants.OverdoseFatalThreshold;
                float seizureChance = isFatal ? EE_Constants.TxaSeizureChanceFatal : EE_Constants.TxaSeizureChanceOverdose;
                float embolismChance = isFatal ? EE_Constants.TxaEmbolismChanceFatal : EE_Constants.TxaEmbolismChanceOverdose;

                // 2.1 判定强直性抽搐 (TXA 癫痫)
                if (Rand.Chance(seizureChance))
                {
                    HediffDef seizureDef = EE_DefOf.EE_TxaSeizure;
                    if (seizureDef != null && !Pawn.health.hediffSet.HasHediff(seizureDef))
                    {
                        Hediff seizure = HediffMaker.MakeHediff(seizureDef, Pawn);
                        seizure.Severity = 1.0f;
                        Pawn.health.AddHediff(seizure, null, null, null);

                        if (Pawn.Spawned && Pawn.Map != null)
                        {
                            MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "高凝毒性 - 强直性抽搐!", UnityEngine.Color.red);
                        }
                    }
                }

                // 2.2 判定血栓栓塞 (肢体缺氧坏死 或 冠状动脉心肌梗死)
                if (Rand.Chance(embolismChance))
                {
                    // 致命剂量下，除了外周血栓，有概率直接引发心肌梗死（冠状动脉栓塞）
                    if (isFatal && Rand.Chance(0.4f))
                    {
                        HediffDef miDef = EE_DefOf.EE_MyocardialInfarction;
                        if (miDef != null)
                        {
                            BodyPartRecord heart = EE_BodyPartCache.GetBloodPumpingSources(Pawn)?.FirstOrDefault();
                            if (heart == null)
                            {
                                heart = Pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def == BodyPartDefOf.Heart);
                            }
                            if (heart == null)
                            {
                                heart = Pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def.defName.IndexOf("heart", System.StringComparison.OrdinalIgnoreCase) >= 0);
                            }

                            if (!Pawn.health.hediffSet.HasHediff(miDef, heart))
                            {
                                // 诱发心梗到心脏部位
                                Hediff mi = HediffMaker.MakeHediff(miDef, Pawn, heart);
                                mi.Severity = 0.05f; // 室颤起步
                                Pawn.health.AddHediff(mi, heart, null, null);

                                if (Pawn.Spawned && Pawn.Map != null)
                                {
                                    MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "高凝冠状动脉栓塞 - 心梗!", UnityEngine.Color.red);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 随机选择一个未缺失的外部肢体或器官，施加组织缺氧
                        BodyPartRecord targetPart = Pawn.health.hediffSet.GetNotMissingParts()
                            .Where(p => p.def.alive && p.depth == BodyPartDepth.Outside)
                            .RandomElementWithFallback(null);

                        if (targetPart != null)
                        {
                            HediffDef hypoxiaDef = EE_DefOf.TissueHypoxia;
                            if (hypoxiaDef != null)
                            {
                                Hediff hypoxia = HediffMaker.MakeHediff(hypoxiaDef, Pawn, targetPart);
                                hypoxia.Severity = 1.0f;
                                Pawn.health.AddHediff(hypoxia, targetPart, null, null);

                                if (Pawn.Spawned && Pawn.Map != null)
                                {
                                    MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, $"微血管栓塞: {targetPart.Label} 缺氧!", UnityEngine.Color.red);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
