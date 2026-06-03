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
        public static HediffDef EE_MyocardialInfarction;
        public static HediffDef AdrenalineBoost;
        public static HediffDef AdrenalineCrash;
        
        // 并发症新增 Def
        public static HediffDef Coagulopathy;
        public static HediffDef SIRS;
        public static HediffDef MultipleOrganFailure;
        
        public static HediffDef EE_BiologicalDeathTimer;
        public static HediffDef EE_BiologicalDeath;
        public static HediffDef EE_DeclaredDead;
        public static HediffDef EE_Pneumothorax;
        public static HediffDef EE_Shock;
        
        // TXA 与 肾上腺素毒理机制 Defs
        public static HediffDef EE_TxaActive;
        public static HediffDef EE_TxaSeizure;
        public static ThingDef EE_TxaSyringe;
        
        // 吗啡毒理与急救机制 Defs
        public static HediffDef EE_MorphineActive;
        public static HediffDef EE_MorphineRespiratoryArrest;
        public static ThingDef EE_MorphineSyringe;
        
        // MODS 细化器官损伤与衰竭 Def
        public static HediffDef EE_MyocardialIschemia;
        public static HediffDef EE_HeartFailure;
        public static HediffDef EE_AcuteRespiratoryDistress;
        public static HediffDef EE_RespiratoryFailure;
        public static HediffDef EE_AcuteLiverInjury;
        public static HediffDef EE_LiverFailure;
        public static HediffDef EE_AcuteKidneyInjury;
        public static HediffDef EE_KidneyFailure;
        
        // 骨折机制新增 Def
        public static HediffDef EE_ClosedFracture;
        public static HediffDef EE_OpenFracture;
        public static HediffDef EE_Malunion;
        
        // 感染机制新增 Def
        public static HediffDef EE_Necrosis;
        public static HediffDef EE_Sepsis;

        public static ThingDef EE_FractureRing;
        public static ThingDef EE_PlasterBandage;

        public static RecipeDef EE_Recipe_TraditionalBoneSetting;
        public static RecipeDef EE_Recipe_PlasterCasting;
        public static RecipeDef EE_Recipe_ORIF;
        public static RecipeDef EE_Recipe_Osteotomy;
        public static RecipeDef EE_Recipe_NeedleDecompression;
        
        public static JobDef EE_ApplyFirstAid;
        public static JobDef EE_PerformCPR;
        
        public static DesignationDef EE_FastFirstAid;
        
        public static HediffDef EE_CPR_Receiving;
        public static ThingDef EE_Defibrillator;
        
        public static BodyPartDef Brain;
        
        // 音效新增 Def
        public static SoundDef EE_BoneCrunch;

        static EE_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EE_DefOf));
            Log.Message("[EmergencyExpanded] EE_DefOf initialized. Version 1.6 - Pneumothorax Patch Active.");
        }
    }
}
