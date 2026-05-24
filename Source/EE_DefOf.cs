using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [DefOf]
    public static class EE_DefOf
    {
        public static HediffDef TissueHypoxia;
        public static HediffDef MetabolicAcidosis;
        public static HediffDef CerebralHypoxia;
        public static HediffDef HypoxicBrainDamage;
        public static HediffDef VegetativeState;
        public static HediffDef ArterialRupture;
        public static HediffDef VentricularFibrillation;
        public static HediffDef AdrenalineBoost;
        public static HediffDef AdrenalineCrash;
        public static HediffDef EE_AdrenalineStabilized;
        
        public static JobDef EE_ApplyFirstAid;
        
        public static BodyPartDef Brain;

        static EE_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EE_DefOf));
        }
    }
}
