using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class Hediff_Fracture : Hediff_Injury
    {
        public bool isSplinted = false;          // 简易硬夹板固定
        public bool isCasted = false;            // 石膏绷带固定
        public bool isInternallyFixed = false;   // 手术钢板内固定
        public bool isStrictBedrest = false;     // 传统正骨静卧固定
        public float alignmentQuality = 0f;      // 骨折复位对齐质量 (0.0 到 1.0)

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isSplinted, "isSplinted", false);
            Scribe_Values.Look(ref isCasted, "isCasted", false);
            Scribe_Values.Look(ref isInternallyFixed, "isInternallyFixed", false);
            Scribe_Values.Look(ref isStrictBedrest, "isStrictBedrest", false);
            Scribe_Values.Look(ref alignmentQuality, "alignmentQuality", 0f);
        }

        // 动态修改 UI 中的伤口标签，展示当前固定状态
        public override string Label
        {
            get
            {
                string baseLabel = base.Label;
                if (isInternallyFixed) return baseLabel + " " + "EE_FractureInternallyFixedTag".Translate();
                if (isCasted) return baseLabel + " " + "EE_FractureCastedTag".Translate();
                if (isStrictBedrest) return baseLabel + " " + "EE_FractureStrictBedrestTag".Translate();
                if (isSplinted) return baseLabel + " " + "EE_FractureSplintedTag".Translate();
                return baseLabel + " " + "EE_FractureUnfixedTag".Translate();
            }
        }

        // 屏蔽自动常规倾向，但向原版系统暴露骨折仍需治疗，以此解决病床静卧与任务完全康复提示的 Bug
        public override bool TendableNow(bool ignoreTimer = false)
        {
            if (isSplinted || isCasted || isInternallyFixed || isStrictBedrest)
            {
                return false;
            }
            return base.TendableNow(ignoreTimer);
        }

        // 动态调控骨折剧痛：固定程度越高，痛觉越弱
        public override float PainOffset
        {
            get
            {
                float basePain = base.PainOffset;
                if (isInternallyFixed) return basePain * 0.15f; // 钢板内固定：完全刚性，痛觉极轻
                if (isCasted) return basePain * 0.30f;          // 石膏固定：良好刚性，痛觉轻微
                if (isStrictBedrest) return basePain * 0.50f;   // 传统正骨静卧：卧床时痛觉中等
                if (isSplinted) return basePain * 0.60f;        // 夹板固定：痛觉中度缓解
                return basePain;                                // 未固定：碎骨摩擦，剧烈疼痛
            }
        }

        private HediffStage cachedStage;
        private bool lastSplinted;
        private bool lastCasted;
        private bool lastInternallyFixed;
        private bool lastStrictBedrest;

        // 通过重载虚属性 CurStage 动态生成和控制骨折在不同阶段对小人行动/操作能力的设定，并使用状态缓存防止 GC 垃圾产生。
        public override HediffStage CurStage
        {
            get
            {
                if (cachedStage == null || 
                    lastSplinted != isSplinted || 
                    lastCasted != isCasted || 
                    lastInternallyFixed != isInternallyFixed || 
                    lastStrictBedrest != isStrictBedrest)
                {
                    lastSplinted = isSplinted;
                    lastCasted = isCasted;
                    lastInternallyFixed = isInternallyFixed;
                    lastStrictBedrest = isStrictBedrest;

                    cachedStage = new HediffStage();
                    cachedStage.capMods = new List<PawnCapacityModifier>();
                    
                    PawnCapacityDef capacity = GetAffectedCapacity();
                    if (capacity != null)
                    {
                        float offsetVal = EE_Constants.FractureCapacityOffsetNone;
                        if (isInternallyFixed) offsetVal = 0.0f;
                        else if (isCasted) offsetVal = EE_Constants.FractureCapacityOffsetCast;
                        else if (isStrictBedrest) offsetVal = EE_Constants.FractureCapacityOffsetBedrest;
                        else if (isSplinted) offsetVal = EE_Constants.FractureCapacityOffsetSplint;

                        if (offsetVal < 0.0f)
                        {
                            cachedStage.capMods.Add(new PawnCapacityModifier
                            {
                                capacity = capacity,
                                offset = offsetVal
                            });
                        }
                    }
                }

                return cachedStage;
            }
        }

        // 定位骨折对小人造成影响的能力类型 (腿部->移动，手臂->操作)
        private PawnCapacityDef GetAffectedCapacity()
        {
            if (Part == null) return null;
            if (Part.def.tags != null)
            {
                if (Part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)) return PawnCapacityDefOf.Moving;
                if (Part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) return PawnCapacityDefOf.Manipulation;
            }
            string name = Part.def.defName.ToLower();
            if (name.Contains("leg") || name.Contains("foot") || name.Contains("tibia") || name.Contains("femur"))
                return PawnCapacityDefOf.Moving;
            if (name.Contains("arm") || name.Contains("hand") || name.Contains("humerus") || name.Contains("radius") || name.Contains("clavicle"))
                return PawnCapacityDefOf.Manipulation;
            return null;
        }

        // 核心 Tick 逻辑：低频检测断骨位移与卧床状态
        public override void PostTick()
        {
            base.PostTick();

            if (pawn.Dead) return;

            // 约每 4.16 秒 (250 ticks) 执行一次判定，极大节省性能
            if (pawn.IsHashIntervalTick(250))
            {
                CheckSecondaryDamage();
                CheckStrictBedrestFailure();

                // 保持绷带视觉效果：如果已固定，则强制使其处于被包扎状态 (刷新 tendTicksLeft)
                if (isSplinted || isCasted || isInternallyFixed || isStrictBedrest)
                {
                    HediffComp_TendDuration tendComp = this.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null && !tendComp.IsTended)
                    {
                        tendComp.tendTicksLeft = int.MaxValue;
                        tendComp.tendQuality = 1.0f;
                        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                    }
                }
            }
        }

        // 检测断骨移位造成的二次软组织损伤
        private void CheckSecondaryDamage()
        {
            // 如果已经被有效固定，则免除二次伤害
            if (isSplinted || isCasted || isInternallyFixed || isStrictBedrest) return;

            // 只有在小人正在移动且未倒地时，才会因为碎骨摩擦戳刺导致二次伤害
            if (pawn.pather != null && pawn.pather.Moving && !pawn.Downed)
            {
                float chance = EE_Settings.SecondaryDamageChance;
                if (Rand.Chance(chance))
                {
                    // 1. 物理伤害判定：对骨骼周围造成微量切伤
                    BodyPartRecord damagePart = Part;
                    if (damagePart != null)
                    {
                        // 取骨骼的父级肢体（如大腿肌肉），使二次伤害更写实
                        if (damagePart.parent != null) damagePart = damagePart.parent;

                        DamageInfo dinfo = new DamageInfo(DamageDefOf.Cut, EE_Constants.FractureSecondaryDamageAmount, 0f, -1f, pawn, damagePart);
                        pawn.TakeDamage(dinfo);
                    }

                    // 2. 视觉与行为惩罚：剧痛踉跄
                    if (pawn.Spawned && pawn.Map != null)
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "EE_MoteFractureDisplacedPain".Translate(pawn.LabelShort), Color.red);
                    }

                    // 瞬间痛觉踉跄
                    if (pawn.stances != null && pawn.stances.stunner != null && !pawn.stances.stunner.Stunned)
                    {
                        pawn.stances.stunner.StunFor(EE_Constants.FractureStunTicks, pawn, false, true);
                    }
                }
            }
        }

        // 检测传统正骨的绝对静卧失效机制
        private void CheckStrictBedrestFailure()
        {
            if (!isStrictBedrest) return;

            // 如果正骨后不老实躺着，而是下床跑动，则有几率导致对齐失败、正骨崩塌
            if (pawn.CurrentBed() == null && pawn.pather != null && pawn.pather.Moving)
            {
                if (Rand.Chance(EE_Constants.FractureStrictBedrestFailChance))
                {
                    isStrictBedrest = false;
                    alignmentQuality = 0f; // 复位质量清零，重新归于错位状态
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                    
                    Messages.Message("EE_MessageStrictBedrestFail".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
                    
                    if (pawn.Spawned && pawn.Map != null)
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "EE_MoteBedrestSettingFail".Translate(), Color.red);
                    }
                }
            }
        }

        // 控制自然愈合速度
        public override void Heal(float amount)
        {
            // 如果骨折没有打石膏、做内固定或正骨静卧，其极难自行完美对齐，愈合速率减慢至 10%
            if (!isCasted && !isInternallyFixed && !isStrictBedrest)
            {
                amount *= 0.10f; 
            }

            float oldSeverity = Severity;
            base.Heal(amount);

            // 当伤势彻底愈合（严重度归零）的前一刻，结算是否发生畸形愈合
            if (oldSeverity > 0f && Severity <= 0f)
            {
                CheckMalunion();
            }
        }

        // 愈合终期结算畸形愈合
        private void CheckMalunion()
        {
            if (Part != null && pawn.health.hediffSet.PartIsMissing(Part)) return;
            
            // 对齐质量过低时（例如没有正骨，仅靠硬夹板或自然愈合），极高概率发生畸形愈合
            if (alignmentQuality < 0.50f)
            {
                float malunionChance = 0.90f;
                // 即便上了硬夹板，也有 60% 畸形率，逼迫玩家更换石膏/手术
                if (isSplinted) malunionChance = 0.60f; 

                if (Rand.Chance(malunionChance))
                {
                    HediffDef malDef = EE_DefOf.EE_Malunion;
                    if (malDef != null && Part != null)
                    {
                        Hediff malunion = HediffMaker.MakeHediff(malDef, pawn, Part);
                        pawn.health.AddHediff(malunion, Part);
                        
                        Messages.Message("EE_MessageMalunionCrippled".Translate(pawn.LabelShort, Part.Label), pawn, MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
            else
            {
                Messages.Message("EE_MessageFractureHealedPerfect".Translate(pawn.LabelShort, Part.Label), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

    }
}
