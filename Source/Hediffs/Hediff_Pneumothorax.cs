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
                if (isDecompressed)
                {
                    return "已减压".Translate();
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
            
            // 手动控制严重度变化，覆盖 XML，因为我们需要减压状态的特殊处理
            if (isDecompressed)
            {
                // 已减压：每天缓慢自愈，大约需要几天时间完全恢复
                this.Severity -= 0.3f / 60000f; 
            }
            else
            {
                // 未减压：张力性气胸快速恶化，每天增加 1.5 严重度 (约16小时即致命)
                this.Severity += 1.5f / 60000f; 
            }
        }
    }
}
