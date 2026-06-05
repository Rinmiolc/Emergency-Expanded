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

        /// <summary>
        /// 计算全身系统性炎性负载 (Trauma Load)。
        /// 融合了缺氧、外伤、败血症、坏死等因素，用于判定 SIRS 的触发与演进。
        /// 过滤了 Severity <= 3.0f 的微型擦伤/伤口，并进行平滑过渡。
        /// </summary>
        public static float CalculateTraumaLoad(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return 0f;

            float traumaLoad = 0f;
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (EE_DefOf.TissueHypoxia != null && hediff.def == EE_DefOf.TissueHypoxia)
                {
                    traumaLoad += hediff.Severity * EE_Constants.SirsWeightTissueHypoxia;
                }
                else if (hediff is Hediff_Injury injury)
                {
                    // 忽略 severity <= 3.0f 的微小抓伤/擦伤对全身系统性炎性负载的影响，防止小动物抓伤累加引发 SIRS
                    if (injury.Severity > 3.0f)
                    {
                        float effectiveSeverity = injury.Severity - 3.0f;
                        traumaLoad += effectiveSeverity * (injury.IsTended() ? EE_Constants.SirsWeightTendedInjury : EE_Constants.SirsWeightUntendedInjury);
                    }
                }
                // 感染引发的强烈炎症
                else if (EE_DefOf.EE_Sepsis != null && hediff.def == EE_DefOf.EE_Sepsis)
                {
                    traumaLoad += hediff.Severity * 40f; 
                }
                else if (EE_DefOf.EE_Necrosis != null && hediff.def == EE_DefOf.EE_Necrosis)
                {
                    traumaLoad += hediff.Severity * 10f;
                }
            }
            return traumaLoad;
        }
    }
}
