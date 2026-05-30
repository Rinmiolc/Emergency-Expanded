using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class Hediff_VentricularFibrillation : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";

        public override void Tick()
        {
            base.Tick();
            // 心肌梗死 100% 进度不再强制致死，交由后续多脏器缺氧衰竭机制处理
        }
    }
}
