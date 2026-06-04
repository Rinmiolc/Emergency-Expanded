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
}
