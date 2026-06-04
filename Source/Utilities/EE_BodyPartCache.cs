using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace EmergencyExpanded
{
    [StaticConstructorOnStartup]
    public static class EE_BodyPartCache
    {
        private static HashSet<BodyPartDef> boneParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> limbParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> majorVesselParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> nonBoneParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> nonLimbParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> nonMajorVesselParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> extremityParts = new HashSet<BodyPartDef>();
        private static HashSet<BodyPartDef> nonExtremityParts = new HashSet<BodyPartDef>();
        private static Dictionary<BodyDef, BodyPartRecord> heartPartCache = new Dictionary<BodyDef, BodyPartRecord>();

        public static bool IsBonePart(BodyPartDef def)
        {
            if (def == null) return false;
            if (boneParts.Contains(def)) return true;
            if (nonBoneParts.Contains(def)) return false;

            bool isBone = false;
            
            if (def.tags != null && def.tags.Any(t => t.defName.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.defName.IndexOf("skeletal", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                isBone = true;
            }
            else
            {
                string name = def.defName;
                if (name.IndexOf("femur", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("tibia", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("humerus", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                    name.IndexOf("radius", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("clavicle", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("spine", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                    name.IndexOf("pelvis", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("rib", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("skull", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                    name.IndexOf("jaw", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isBone = true;
                }
            }

            if (isBone) boneParts.Add(def);
            else nonBoneParts.Add(def);

            return isBone;
        }

        public static bool IsLimbPart(BodyPartDef def)
        {
            if (def == null) return false;
            if (limbParts.Contains(def)) return true;
            if (nonLimbParts.Contains(def)) return false;

            bool isLimb = false;
            if (def.tags != null && (def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)))
            {
                isLimb = true;
            }
            else
            {
                string name = def.defName;
                if (name.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("leg", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isLimb = true;
                }
            }

            if (isLimb) limbParts.Add(def);
            else nonLimbParts.Add(def);

            return isLimb;
        }

        public static bool IsMajorVesselPart(BodyPartDef def, Pawn pawn)
        {
            if (def == null) return false;
            
            // Fast check for core part which depends on pawn race props
            if (pawn != null && pawn.RaceProps != null && pawn.RaceProps.body != null && pawn.RaceProps.body.corePart.def == def) return true;

            if (majorVesselParts.Contains(def)) return true;
            if (nonMajorVesselParts.Contains(def)) return false;

            bool isVessel = false;
            if (def == BodyPartDefOf.Neck)
            {
                isVessel = true;
            }
            else if (def.tags != null && (def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)))
            {
                isVessel = true;
            }
            else
            {
                string name = def.defName;
                if (name.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("leg", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                    name.IndexOf("shoulder", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("thigh", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isVessel = true;
                }
            }

            if (isVessel) majorVesselParts.Add(def);
            else nonMajorVesselParts.Add(def);

            return isVessel;
        }

        private static Dictionary<string, Dictionary<BodyPartDef, bool>> organCache = new Dictionary<string, Dictionary<BodyPartDef, bool>>();

        public static bool IsOrganType(BodyPartDef def, BodyPartTagDef tag, string fallbackKeyword)
        {
            if (def == null) return false;
            
            if (!organCache.TryGetValue(fallbackKeyword, out var specificCache))
            {
                specificCache = new Dictionary<BodyPartDef, bool>();
                organCache[fallbackKeyword] = specificCache;
            }

            if (specificCache.TryGetValue(def, out bool result))
            {
                return result;
            }

            bool match = false;
            if (tag != null && def.tags != null && def.tags.Contains(tag)) match = true;
            if (!match && !string.IsNullOrEmpty(fallbackKeyword) && def.defName.IndexOf(fallbackKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0) match = true;
            
            specificCache[def] = match;
            return match;
        }

        private static Dictionary<BodyDef, List<BodyPartRecord>> bloodPumpingSourcesCache = new Dictionary<BodyDef, List<BodyPartRecord>>();

        public static List<BodyPartRecord> GetBloodPumpingSources(Pawn pawn)
        {
            if (pawn?.RaceProps?.body == null) return null;
            BodyDef body = pawn.RaceProps.body;
            
            if (bloodPumpingSourcesCache.TryGetValue(body, out List<BodyPartRecord> parts))
            {
                return parts;
            }
            
            parts = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in body.AllParts)
            {
                if (part.def.tags != null && part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource))
                {
                    parts.Add(part);
                }
            }
            bloodPumpingSourcesCache[body] = parts;
            return parts;
        }

        public static bool IsExtremityPart(BodyPartRecord part)
        {
            if (part == null || part.def == null) return false;
            BodyPartDef def = part.def;
            if (extremityParts.Contains(def)) return true;
            if (nonExtremityParts.Contains(def)) return false;

            bool isExtremity = false;
            if (def.tags != null && (def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment) || def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment)))
            {
                if (part.parts == null || part.parts.Count == 0)
                {
                    isExtremity = true;
                }
            }

            if (!isExtremity)
            {
                string defNameLower = def.defName.ToLower();
                if (defNameLower.Contains("finger") || defNameLower.Contains("toe") || 
                    defNameLower.Contains("nose") || defNameLower.Contains("ear"))
                {
                    isExtremity = true;
                }
            }

            if (isExtremity) extremityParts.Add(def);
            else nonExtremityParts.Add(def);

            return isExtremity;
        }

        public static BodyPartRecord GetHeartPart(Pawn pawn)
        {
            if (pawn?.RaceProps?.body == null) return null;
            BodyDef body = pawn.RaceProps.body;
            if (heartPartCache.TryGetValue(body, out BodyPartRecord heart))
            {
                return heart;
            }

            foreach (BodyPartRecord part in body.AllParts)
            {
                if (part.def.tags != null && part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource))
                {
                    heart = part;
                    break;
                }
            }
            if (heart == null)
            {
                foreach (BodyPartRecord part in body.AllParts)
                {
                    if (part.def == BodyPartDefOf.Heart)
                    {
                        heart = part;
                        break;
                    }
                }
            }
            if (heart == null)
            {
                foreach (BodyPartRecord part in body.AllParts)
                {
                    if (part.def != null && part.def.defName.IndexOf("heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        heart = part;
                        break;
                    }
                }
            }

            heartPartCache[body] = heart;
            return heart;
        }
    }
}
