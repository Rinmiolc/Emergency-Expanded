using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // 通用的百分比显示 Hediff 类
    public class Hediff_PercentageDisplay : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";
    }
}
