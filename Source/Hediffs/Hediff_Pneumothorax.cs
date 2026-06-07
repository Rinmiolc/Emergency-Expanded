using RimWorld;
using Verse;
using UnityEngine;

namespace EmergencyExpanded
{
    public class Hediff_Pneumothorax : HediffWithComps
    {
        public bool isDecompressed = false;

        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";

        public override string LabelInBrackets
        {
            get
            {
                if (Patch_HealthCardUtility_UI.isDrawingHealthTab)
                {
                    return base.LabelInBrackets;
                }
                if (isDecompressed)
                {
                    return "EE_PneumothoraxDecompressedTag".Translate();
                }
                return base.LabelInBrackets;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isDecompressed, "isDecompressed", false);
        }

        public override void Tick()
        {
            base.Tick();
            
            if (pawn == null || pawn.Dead) return;

            // 每 60 Ticks 更新一次，稀疏化 Severity 变动以节省性能并减少属性变更通知频次
            if (pawn.IsHashIntervalTick(60))
            {
                // 手动控制严重度变化，覆盖 XML，因为我们需要减压状态的特殊处理
                if (isDecompressed)
                {
                    // 已减压：每天缓慢自愈，大约需要几天时间完全恢复 (0.3 / 1000f per 60 ticks)
                    this.Severity -= 0.3f / 1000f; 
                }
                else
                {
                    var tendComp = this.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null && tendComp.IsTended)
                    {
                        // 被急救包/医药临时处理：伤情缓解，每天降低 0.5 严重度，但不低于 0.2 (中度气胸)，无法根治
                        if (this.Severity > 0.2f)
                        {
                            this.Severity -= 0.5f / 1000f; 
                        }
                    }
                    else
                    {
                        // 未减压且未临时包扎：张力性气胸快速恶化，每天增加 1.5 严重度
                        this.Severity += 1.5f / 1000f; 
                    }
                }
            }
        }
    }
}
