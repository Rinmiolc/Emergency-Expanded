using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Hediff_InjuryBurn : Hediff_Injury
    {
        public override float PainOffset
        {
            get
            {
                float basePain = base.PainOffset;
                
                HediffComp_Burn burnComp = this.TryGetComp<HediffComp_Burn>();
                if (burnComp != null && burnComp.BurnDegree == 3)
                {
                    // 三度烧伤全层皮肤与神经末梢坏死。
                    // 虽然伤口边缘（相当于II度烧伤区域）依然剧痛，但中心区域是无痛的。
                    // 因此，不管三度烧伤的总面积（Severity）多大，其造成的最大痛楚被限制。
                    // 相当于限制在 12 点 Severity 产生的痛楚，避免严重烧伤叠加出不科学的无限痛楚。
                    float maxPain = 12f * this.def.injuryProps.painPerSeverity;
                    if (basePain > maxPain)
                    {
                        return maxPain;
                    }
                }
                return basePain;
            }
        }
    }

    public class HediffCompProperties_Burn : HediffCompProperties
    {
        public HediffCompProperties_Burn()
        {
            this.compClass = typeof(HediffComp_Burn);
        }
    }

    public class HediffComp_Burn : HediffComp
    {
        public int BurnDegree
        {
            get
            {
                if (parent.Severity >= EE_Constants.BurnDegree3Threshold) return 3;
                if (parent.Severity >= EE_Constants.BurnDegree2Threshold) return 2;
                return 1;
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                int degree = BurnDegree;
                if (degree == 3) return "EE_BurnDegree3".Translate();
                if (degree == 2) return "EE_BurnDegree2".Translate();
                return "EE_BurnDegree1".Translate();
            }
        }
        
        public override string CompTipStringExtra
        {
            get
            {
                int degree = BurnDegree;
                if (degree == 3) return "EE_BurnDegree3Desc".Translate();
                if (degree == 2) return "EE_BurnDegree2Desc".Translate();
                return "EE_BurnDegree1Desc".Translate();
            }
        }
        
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.pawn == null || parent.pawn.Dead) return;
            if (!parent.pawn.IsHashIntervalTick(120)) return;
            
            // 如果是大面积烧伤且未包扎/持续流失体液，引发休克
            if (BurnDegree >= 2 && parent.Severity > 10f && !parent.pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Shock))
            {
                Hediff shock = HediffMaker.MakeHediff(EE_DefOf.EE_Shock, parent.pawn, null);
                parent.pawn.health.AddHediff(shock, null, null, null);
            }
        }
    }
}
