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
            return EE_BodyPartCache.IsMajorVesselPart(part.def, pawn);
        }

        /// <summary>
        /// 寻找一个部位最近的、未缺失的祖先部位。
        /// 主要是为了防止将“大出血”等Hediff直接加在已缺失（PartIsMissing）的断肢上，
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
