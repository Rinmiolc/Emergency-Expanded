using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public static class EE_MedicalUtility
    {
        /// <summary>
        /// 判定受击的身体部位是否包含大血管（如颈动脉、肱动脉、股动脉、主动脉群）。
        /// 支持标签判断和字符串回退匹配，具有极高的Mod兼容性。
        /// </summary>
        public static bool IsMajorVesselPart(BodyPartRecord part, Pawn pawn)
        {
            if (part == null || pawn == null) return false;

            // 1. 核心躯干 (主动脉群)
            if (part == pawn.RaceProps.body.corePart) return true;

            // 2. 脖子 (颈总动脉)
            if (part.def == BodyPartDefOf.Neck) return true;

            // 3. 主要肢体核心 (手臂和大腿根部，即肱动脉与股动脉)
            if (part.def.tags != null)
            {
                if (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || 
                    part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore))
                {
                    return true;
                }
            }

            // 4. 后备字符串模糊判断，确保兼容某些未规范使用标签的异形/非标肢体Mod
            string defNameLower = part.def.defName.ToLower();
            if (defNameLower.Contains("arm") || defNameLower.Contains("leg") || 
                defNameLower.Contains("shoulder") || defNameLower.Contains("thigh"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 寻找一个部位最近的、未缺失的祖先部位。
        /// 主要是为了防止将“动脉破裂”等Hediff直接加在已缺失（PartIsMissing）的断肢上，
        /// 从而触发原版游戏的错误日志（Tried to add hediff to missing part...）。
        /// </summary>
        public static BodyPartRecord GetNearestNonMissingPart(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null) return part;
            
            BodyPartRecord current = part;
            while (current != null)
            {
                if (!pawn.health.hediffSet.PartIsMissing(current))
                {
                    return current;
                }
                current = current.parent;
            }
            return pawn.RaceProps.body.corePart; // 终极回退到核心躯干
        }
    }
}
