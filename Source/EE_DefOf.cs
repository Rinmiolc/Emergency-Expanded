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
        public static HediffDef MassiveBleeding;
        public static HediffDef VentricularFibrillation;
        public static HediffDef AdrenalineBoost;
        public static HediffDef AdrenalineCrash;
        
        // 骨折机制新增 Def
        public static HediffDef EE_ClosedFracture;
        public static HediffDef EE_OpenFracture;
        public static HediffDef EE_Malunion;

        public static ThingDef EE_PrimitiveSplint;
        public static ThingDef EE_PlasterBandage;

        public static RecipeDef EE_Recipe_TraditionalBoneSetting;
        public static RecipeDef EE_Recipe_PlasterCasting;
        public static RecipeDef EE_Recipe_ORIF;
        public static RecipeDef EE_Recipe_Osteotomy;
        
        public static JobDef EE_ApplyFirstAid;
        
        public static DesignationDef EE_FastFirstAid;
        
        public static BodyPartDef Brain;

        static EE_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EE_DefOf));
        }
    }
}
