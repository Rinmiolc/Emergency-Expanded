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
            if (pawn == null || pawn.Dead) return;

            // 如果病情恶化到 100% (1.0f)，代表心肌坏死，直接导致不可逆的临床死亡
            if (this.Severity >= 1.0f)
            {
                pawn.Kill(null, this);
            }
        }
    }
}
