using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace EmergencyExpanded
{
    // ================= 1. 核心状态缓存数据类 =================
    public class CachedVitals
    {
        public int lastUpdateTick = -999;
        public float heartRate = 70f;
        public float displayHeartRate = 70f;
        
        // UI 渲染专用状态
        public float[] waveBuffer = new float[160];
        public float lastTime = -1f;
        public float phase = 0f;
        public float sweepX = 0f;
        public float displaySpO2 = 98f;
        public float lastShockTime = -999f;
        
        // 缓存的病理状态，每 120 ticks 更新一次
        public bool hasCerebralHypoxia = false;
        public bool hasMetabolicAcidosis = false;
        public bool hasMyocardialInfarction = false;
        public bool hasArrhythmia = false;

        public float displaypH = 7.40f;
        public float displayRR = 16f;
        public float displayTemp = 37.0f;
    }

    // ================= 2. 动态生理心搏计算管理器 =================
    public static class VitalTracker
    {
        // 采用 ConditionalWeakTable 绑定小人与心率数据，保证 100% 内存防泄漏与零垃圾回收开销
        private static readonly ConditionalWeakTable<Pawn, CachedVitals> cachedTable = new ConditionalWeakTable<Pawn, CachedVitals>();

        public static CachedVitals GetOrCreateVitals(Pawn pawn)
        {
            if (pawn == null) return null;
            return cachedTable.GetValue(pawn, p => new CachedVitals());
        }

        public static void TriggerDefibrillatorShock(Pawn pawn)
        {
            CachedVitals vitals = GetOrCreateVitals(pawn);
            if (vitals != null)
            {
                vitals.lastShockTime = Time.realtimeSinceStartup;
            }
        }

        // 动态生理计算心率
        public static float CalculateDynamicHeartRate(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return 0f;

            // 0. 优先短路：如果心脏物理缺失，则心搏立刻归零 (Flatline)
            var pumpingSources = EE_BodyPartCache.GetBloodPumpingSources(pawn);
            bool isHeartMissing = false;
            if (pumpingSources != null)
            {
                for (int i = 0; i < pumpingSources.Count; i++)
                {
                    if (!pawn.health.hediffSet.GetNotMissingParts().Contains(pumpingSources[i]))
                    {
                        isHeartMissing = true;
                        break;
                    }
                }
            }
            if (isHeartMissing) return 0f;

            // 1. 根据生理年龄计算基准静息心率 (年轻略快，老年略慢)
            float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;
            float baseHR = 80f - Mathf.Clamp(bioAge - 15f, 0f, 60f) * 0.2f; // 人类范围在 68 - 80 左右

            // 2. 肾上腺素加成 (根据 1.0/2.0/3.0 阶段分级加算心率)
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost);
            if (adrenaline != null)
            {
                float sev = adrenaline.Severity;
                if (sev <= 1.0f)
                {
                    // 正常区间：最高 +40 bpm
                    baseHR += sev * 40f;
                }
                else if (sev <= 2.0f)
                {
                    // 过量区间：+40 ~ +80 bpm
                    baseHR += 40f + (sev - 1.0f) * 40f;
                }
                else
                {
                    // 致命区间：+80 ~ +150 bpm (儿茶酚胺风暴)
                    baseHR += 80f + (sev - 2.0f) * 70f;
                }
            }

            // 吗啡抑制心率 (根据 1.0/2.0/3.0 阶段分级扣减心率)
            if (EE_DefOf.EE_MorphineActive != null)
            {
                Hediff morphine = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MorphineActive);
                if (morphine != null)
                {
                    float sev = morphine.Severity;
                    if (sev <= 1.0f)
                    {
                        baseHR -= sev * 15f;
                    }
                    else if (sev <= 2.0f)
                    {
                        baseHR -= 15f + (sev - 1.0f) * 15f;
                    }
                    else
                    {
                        baseHR -= 30f + (sev - 2.0f) * 15f;
                    }
                }
            }

            // 3. 急性流血引起的交感神经代偿 (急性失血交感亢奋，心率立刻快速飙升)
            float bleedRate = pawn.health.hediffSet.BleedRateTotal;
            if (bleedRate > 0.01f)
            {
                baseHR += Mathf.Clamp(bleedRate * 35f, 0f, 60f); // 突击大出血立刻增加 20-60 bpm
            }

            // 4. 累积失血状态代偿与崩溃 (失容量代偿)
            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                float severity = bloodLoss.Severity;
                if (severity < 0.6f)
                {
                    // 轻中度失血：强烈的代偿性心动过速
                    baseHR += severity * 80f; // 0.6 严重度时增加 48 bpm
                }
                else if (severity < 0.8f)
                {
                    // 重度失血：极限代偿
                    baseHR += 48f + (severity - 0.6f) * 160f; // 0.8 严重度时增加 80 bpm，心率冲至 150-160
                }
                else
                {
                    // 终末脱水休克：心肌缺氧坏死，代偿崩溃，心率灾难性慢阻跌落
                    baseHR = Mathf.Lerp(baseHR, 30f, (severity - 0.8f) / 0.2f);
                }
            }

            // 5. 心律失常与骤停状态判定
            Hediff vFib = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
            if (vFib != null)
            {
                if (vFib.Severity < 0.60f)
                {
                    // 致命心室颤动：狂乱颤动，频率极速且无规则
                    baseHR = 220f + Rand.Range(-20f, 20f);
                }
                else
                {
                    // 致命心跳骤停：心搏归零 (Flatline)
                    baseHR = 0f;
                }
            }

            // 6. 睡眠状态下心率生理性下降 (约下降 12 bpm)
            if (!pawn.Awake())
            {
                baseHR -= 12f;
            }

            return Mathf.Clamp(baseHR, 0f, 280f);
        }

        // 每两秒 (120 ticks) 更新一次数值，防性能消耗
        public static void UpdateVitalsIfNeed(Pawn pawn, CachedVitals vitals)
        {
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - vitals.lastUpdateTick >= 120 || vitals.lastUpdateTick == -999)
            {
                vitals.lastUpdateTick = currentTick;
                vitals.heartRate = CalculateDynamicHeartRate(pawn);
                
                // 更新病理状态缓存，避免每帧在 UI 渲染中遍历 Hediff 列表
                vitals.hasCerebralHypoxia = pawn.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia);
                vitals.hasMetabolicAcidosis = pawn.health.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis);
                vitals.hasMyocardialInfarction = pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction);
                vitals.hasArrhythmia = pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Arrhythmia);
                
                if (pawn.Dead)
                {
                    vitals.displayHeartRate = 0f;
                    vitals.displaySpO2 = 0f;
                    vitals.displaypH = 7.0f; // Post-mortem acidosis
                    vitals.displayRR = 0f;

                    // 尸冷 (Algor Mortis): 尸体温度每2秒向环境温度靠拢 0.1 度
                    float ambient = pawn.AmbientTemperature;
                    if (vitals.displayTemp > ambient)
                    {
                        vitals.displayTemp = Mathf.Max(vitals.displayTemp - 0.1f, ambient);
                    }
                    else if (vitals.displayTemp < ambient)
                    {
                        vitals.displayTemp = Mathf.Min(vitals.displayTemp + 0.1f, ambient);
                    }
                }
                else
                {
                    // 1. 心率显示与波动
                    if (vitals.heartRate > EE_Constants.EcgFlatlineThreshold)
                    {
                        vitals.displayHeartRate = vitals.heartRate + Rand.Range(-2f, 2f);
                        vitals.displayHeartRate = Mathf.Clamp(vitals.displayHeartRate, 10f, 280f);
                    }
                    else
                    {
                        vitals.displayHeartRate = 0f;
                    }

                    // 2. 高拟真电生理 SpO2 血氧饱和度算法
                    float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                    float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                    
                    // 如果心搏停止或微弱，则供氧循环也停止
                    if (vitals.displayHeartRate < EE_Constants.EcgFlatlineThreshold)
                    {
                        pumping = 0f;
                    }

                    float acidosisSeverity = 0f;
                    Hediff acidosis = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
                    if (acidosis != null)
                    {
                        acidosisSeverity = acidosis.Severity;
                    }

                    float capacityIndex = Mathf.Min(Mathf.Clamp01(breathing), Mathf.Clamp01(pumping));
                    float targetSpO2 = 98f;

                    // 建立血氧饱和度与脏器功能 (容量/灌注) 的精细数学模型
                    if (capacityIndex >= 0.9f)
                    {
                        targetSpO2 = Mathf.Lerp(95f, 99f, (capacityIndex - 0.9f) / 0.1f) + Rand.Range(-0.5f, 0.5f);
                    }
                    else if (capacityIndex >= 0.75f)
                    {
                        targetSpO2 = Mathf.Lerp(90f, 95f, (capacityIndex - 0.75f) / 0.15f);
                    }
                    else if (capacityIndex >= 0.5f)
                    {
                        targetSpO2 = Mathf.Lerp(75f, 90f, (capacityIndex - 0.5f) / 0.25f);
                    }
                    else if (capacityIndex >= 0.3f)
                    {
                        targetSpO2 = Mathf.Lerp(50f, 75f, (capacityIndex - 0.3f) / 0.2f);
                    }
                    else
                    {
                        targetSpO2 = Mathf.Lerp(0f, 50f, capacityIndex / 0.3f);
                    }

                    targetSpO2 -= acidosisSeverity * 10f; // 酸中毒负荷加重血氧剥离
                    targetSpO2 = Mathf.Clamp(targetSpO2, 0f, 99.4f);

                    // 逐步拟真向目标值逼近（模拟氧消耗与储备耗尽延迟）
                    if (vitals.displaySpO2 < targetSpO2)
                    {
                        float recoveryRate = 3f; // 2秒回升 3%
                        vitals.displaySpO2 = Mathf.Min(vitals.displaySpO2 + recoveryRate, targetSpO2);
                    }
                    else if (vitals.displaySpO2 > targetSpO2)
                    {
                        float diff = vitals.displaySpO2 - targetSpO2;
                        float declineRate = 2f + (diff * 0.15f); // 差值越大，下降越剧烈，模拟无氧储备瞬间耗尽
                        vitals.displaySpO2 = Mathf.Max(vitals.displaySpO2 - declineRate, targetSpO2);
                    }

                    // 3. 计算并缓存血液酸碱度 (pH)
                    float ph = 7.40f;
                    if (acidosis != null)
                    {
                        ph -= acidosis.Severity * 0.55f;
                    }
                    ph += Rand.Range(-0.01f, 0.01f);
                    vitals.displaypH = Mathf.Clamp(ph, 6.70f, 7.45f);

                    // 4. 计算并缓存呼吸频率 (RR)
                    float rr = 16f;
                    if (breathing <= 0.05f)
                    {
                        rr = 0f;
                    }
                    else
                    {
                        rr = 16f * breathing;
                        if (acidosis != null) rr += acidosis.Severity * 14f;
                        Hediff morphine = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MorphineActive);
                        if (morphine != null) rr -= morphine.Severity * 6f;
                        float totalBleed = pawn.health.hediffSet.BleedRateTotal;
                        if (totalBleed > 0.1f)
                        {
                            rr += Mathf.Clamp(totalBleed * 8f, 0f, 12f);
                        }
                        rr += Rand.Range(-1f, 1f);
                        rr = Mathf.Clamp(rr, 0f, 45f);
                    }
                    vitals.displayRR = rr;

                    // 5. 计算并缓存体温 (Body Temperature)
                    float bodyTemp = 37.0f;
                    
                    // 免疫/感染发热
                    float fever = 0f;
                    if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Sepsis))
                    {
                        fever = 1.5f + pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Sepsis).Severity * 1.5f; // +1.5 to +3.0 C
                    }
                    else if (pawn.health.hediffSet.HasHediff(HediffDef.Named("Flu")) || pawn.health.hediffSet.HasHediff(HediffDef.Named("Plague")))
                    {
                        fever = 1.2f;
                    }
                    bodyTemp += fever;

                    // 失温症 (Hypothermia)
                    Hediff hypothermia = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                    if (hypothermia != null)
                    {
                        bodyTemp -= hypothermia.Severity * 12f; // 降至最低 25 C
                    }

                    // 热射病 (Heatstroke)
                    Hediff heatstroke = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
                    if (heatstroke != null)
                    {
                        bodyTemp += heatstroke.Severity * 5.2f; // 升至最高 42.2 C
                    }

                    // 肾上腺素轻微升温
                    if (pawn.health.hediffSet.HasHediff(EE_DefOf.AdrenalineBoost))
                    {
                        bodyTemp += pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost).Severity * 0.4f;
                    }

                    // 随机微小生理波动
                    bodyTemp += Rand.Range(-0.1f, 0.1f);
                    bodyTemp = Mathf.Clamp(bodyTemp, 20f, 43f);
                    vitals.displayTemp = bodyTemp;
                }
            }
        }

        public static void UpdateWaveform(CachedVitals vitals, float bpm)
        {
            float t = Time.realtimeSinceStartup;
            if (vitals.lastTime < 0f) vitals.lastTime = t;
            float dt = t - vitals.lastTime;
            
            if (dt <= 0f) return; // Already updated this frame
            if (dt > 0.1f) dt = 0.1f;
            
            vitals.lastTime = t;

            float virtualWidth = 160f; // Fixed virtual width for uniform simulation

            float beatDuration = (bpm > EE_Constants.EcgFlatlineThreshold) ? (60f / bpm) : 1f;
            if (vitals.hasArrhythmia)
            {
                beatDuration += Mathf.Sin(t * 3f) * (beatDuration * 0.25f);
            }
            float sweepSpeed = virtualWidth / 2.4f;
            
            int oldX = Mathf.FloorToInt(vitals.sweepX);
            vitals.sweepX += dt * sweepSpeed;
            if (vitals.sweepX >= virtualWidth) vitals.sweepX -= virtualWidth;
            int newX = Mathf.FloorToInt(vitals.sweepX);

            float phaseDelta = (bpm > EE_Constants.EcgFlatlineThreshold) ? (dt / beatDuration) : 0f;
            float newPhase = vitals.phase + phaseDelta;

            int stepPixels = newX - oldX;
            if (stepPixels < 0) stepPixels += Mathf.FloorToInt(virtualWidth);

            if (stepPixels > 0)
            {
                for (int step = 1; step <= stepPixels; step++)
                {
                    int x = (oldX + step) % Mathf.FloorToInt(virtualWidth);
                    float fraction = (float)step / stepPixels;
                    float p = (vitals.phase + phaseDelta * fraction) % 1f;
                    
                    float val = 0f;
                    float timeAtPixel = t - dt * (1f - fraction);

                    if (vitals.lastShockTime > 0f && timeAtPixel >= vitals.lastShockTime && timeAtPixel < vitals.lastShockTime + 0.6f)
                    {
                        float shockDt = timeAtPixel - vitals.lastShockTime;
                        if (shockDt < 0.05f) val = (shockDt / 0.05f) * 2.5f;
                        else if (shockDt < 0.15f) val = 2.5f - ((shockDt - 0.05f) / 0.1f) * 4.5f;
                        else if (shockDt < 0.35f) val = -2.0f + ((shockDt - 0.15f) / 0.2f) * 2.0f;
                        else val = Mathf.Sin((shockDt - 0.35f) * 15f) * 0.1f * (0.6f - shockDt);
                        
                        val = Mathf.Clamp(val, -1.15f, 1.15f);
                    }
                    else if (bpm < EE_Constants.EcgFlatlineThreshold)
                    {
                        val = Mathf.Sin(timeAtPixel * 1.8f) * 0.024f + Rand.Range(-0.02f, 0.02f);
                    }
                    else if (vitals.hasMyocardialInfarction && bpm > 180f)
                    {
                        float amplitudeMod = 0.75f + Mathf.Sin(timeAtPixel * 7.5f) * 0.25f;
                        float baseWave = Mathf.Sin(timeAtPixel * 37f) * 0.32f + 
                                         Mathf.Cos(timeAtPixel * 79f) * 0.18f + 
                                         Mathf.Sin(timeAtPixel * 131f) * 0.10f;
                        float noise = Rand.Range(-0.08f, 0.08f);
                        val = baseWave * amplitudeMod + noise;
                    }
                    else
                    {
                        bool isHypoxic = vitals.hasCerebralHypoxia || bpm > EE_Constants.EcgTachycardiaThreshold;
                        val = GetBaseECGValue(p, isHypoxic, vitals.hasMyocardialInfarction, bpm);
                        
                        float pPrev = (vitals.phase + phaseDelta * (step - 1f) / stepPixels) % 1f;
                        if (pPrev < p)
                        {
                            float beatDuration_AA = (bpm > 0.1f) ? (60f / bpm) : 1f;
                            float activeFraction_AA = Mathf.Clamp(0.45f / beatDuration_AA, 0.1f, 0.95f);
                            float pPeakR = (0.23f / 0.55f) * activeFraction_AA;
                            float pPeakS = (0.26f / 0.55f) * activeFraction_AA;

                            if (pPrev < pPeakR && p >= pPeakR) val = 1.20f;
                            else if (pPrev < pPeakS && p >= pPeakS) val = -0.35f;
                        }
                    }
                    if (x >= 0 && x < vitals.waveBuffer.Length)
                        vitals.waveBuffer[x] = val;
                }
            }
            vitals.phase = newPhase % 1f;
        }

        public static float GetBaseECGValue(float p, bool isHypoxic, bool isMI, float bpm)
        {
            float beatDuration = (bpm > 0.1f) ? (60f / bpm) : 1f;
            float activeDuration = 0.45f;
            float activeFraction = Mathf.Clamp(activeDuration / beatDuration, 0.1f, 0.95f);
            
            if (p > activeFraction) return 0f;
            
            float origP = (p / activeFraction) * 0.55f;
            
            if (origP < 0.05f) return 0f;
            if (origP < 0.13f) return Mathf.Sin(((origP - 0.05f) / 0.08f) * Mathf.PI) * 0.12f;
            if (origP < 0.18f) return 0f;
            if (origP < 0.20f) return -((origP - 0.18f) / 0.02f) * 0.15f;
            if (origP < 0.23f) return -0.15f + ((origP - 0.20f) / 0.03f) * 1.35f;
            if (origP < 0.26f) return 1.20f - ((origP - 0.23f) / 0.03f) * 1.55f;
            
            if (origP < 0.32f) 
            {
                float stBase = -0.35f + ((origP - 0.26f) / 0.06f) * 0.35f;
                if (isMI) return stBase + 0.22f;
                return isHypoxic ? (stBase - 0.08f) : stBase;
            }
            
            if (origP < 0.55f) 
            {
                float tPhase = (origP - 0.32f) / 0.23f;
                float tWave = Mathf.Pow(Mathf.Sin(tPhase * Mathf.PI), 1.5f) * 0.28f;
                if (isMI) return tWave * 1.5f + 0.15f;
                if (isHypoxic) return -tWave * 0.8f - 0.05f; 
                return tWave;
            }
            return 0f;
        }
    }

    // ================= 3. Custom Gizmo 生命体征检测仪 =================
    public class Gizmo_VitalMonitor : Gizmo
    {
        private readonly Pawn pawn;

        public override float Order => -100f; // 在面板中优先排在最左侧

        public Gizmo_VitalMonitor(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public override float GetWidth(float maxWidth) => 175f; // 黄金比例加长型检测仪，优化排版空间

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            if (pawn != null)
            {
                if (Find.Selector.SingleSelectedThing != pawn)
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(pawn);
                }
                Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
                
                var inspectTab = Find.MainTabsRoot.OpenTab?.TabWindow as MainTabWindow_Inspect;
                if (inspectTab != null)
                {
                    inspectTab.OpenTabType = typeof(ITab_Pawn_Health_EE);
                }
            }
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            
            CachedVitals vitals = VitalTracker.GetOrCreateVitals(pawn);
            if (vitals == null) return new GizmoResult(GizmoState.Clear);

            VitalTracker.UpdateVitalsIfNeed(pawn, vitals);
            float bpm = vitals.displayHeartRate;
            int spo2 = Mathf.RoundToInt(vitals.displaySpO2);

            Color gridColor;
            Color coreColor;
            Color glowColor;

            Color colorCrimson = new Color(0.83f, 0.25f, 0.25f);
            Color colorAmber = new Color(0.85f, 0.60f, 0.25f);
            Color colorMint = new Color(0.32f, 0.78f, 0.52f);

            if (bpm < EE_Constants.EcgFlatlineThreshold)
            {
                gridColor = new Color(0.40f, 0.0f, 0.0f, 0.15f);
                coreColor = colorCrimson;
                glowColor = new Color(0.83f, 0.25f, 0.25f, 0.4f);
            }
            else if (bpm > EE_Constants.EcgTachycardiaThreshold || bpm < (pawn.Awake() ? EE_Constants.EcgBradycardiaThreshold : 35f) || vitals.hasCerebralHypoxia || vitals.hasMetabolicAcidosis)
            {
                gridColor = new Color(0.40f, 0.25f, 0.0f, 0.15f);
                coreColor = colorAmber;
                glowColor = new Color(0.85f, 0.60f, 0.25f, 0.4f);
            }
            else
            {
                gridColor = new Color(0.12f, 0.38f, 0.22f, 0.15f);
                coreColor = colorMint;
                glowColor = new Color(0.32f, 0.78f, 0.52f, 0.4f);
            }

            Rect innerScreen = rect.ContractedBy(4f);
            Widgets.DrawBoxSolid(innerScreen, new Color(0.02f, 0.02f, 0.025f, 1f));

            // 绘制精细毫米级网格
            GUI.color = gridColor;
            // 粗横线
            for (int i = 1; i < 4; i++)
            {
                float y = innerScreen.y + (innerScreen.height / 4f) * i;
                Widgets.DrawLine(new Vector2(innerScreen.x, y), new Vector2(innerScreen.xMax, y), GUI.color, 1f);
            }
            // 粗竖线
            for (int i = 1; i < 8; i++)
            {
                float x = innerScreen.x + (innerScreen.width / 8f) * i;
                Widgets.DrawLine(new Vector2(x, innerScreen.y), new Vector2(x, innerScreen.yMax), GUI.color, 1f);
            }
            // 辅助细线
            GUI.color = gridColor * new Color(1f, 1f, 1f, 0.35f);
            for (int i = 1; i < 8; i++)
            {
                float y = innerScreen.y + (innerScreen.height / 8f) * i;
                Widgets.DrawLine(new Vector2(innerScreen.x, y), new Vector2(innerScreen.xMax, y), GUI.color, 1f);
            }
            GUI.color = Color.white;

            Rect waveRect = new Rect(innerScreen.x + 2f, innerScreen.y + 20f, innerScreen.width - 48f, 46f);
            float centerY = waveRect.y + waveRect.height / 2f;
            float waveWidth = waveRect.width;

            if (Event.current.type == EventType.Repaint)
            {
                VitalTracker.UpdateWaveform(vitals, bpm);
            }

            int bufferLen = vitals.waveBuffer.Length;
            float scaleX = waveWidth / (bufferLen - 1);

            int drawPoints = bufferLen - 1;
            for (int i = 0; i < drawPoints; i++)
            {
                float virtualWaveWidth = 160f;
                float distToSweep = (i * (virtualWaveWidth / bufferLen)) - vitals.sweepX;
                if (distToSweep < 0f) distToSweep += virtualWaveWidth;
                
                float alpha = 1f;
                if (distToSweep < 14f)
                {
                    alpha = distToSweep / 14f; 
                }
                
                float currentSweepIndex = vitals.sweepX * (bufferLen / virtualWaveWidth);
                if (i + 1 > currentSweepIndex && i <= currentSweepIndex) continue;
                
                float v1 = vitals.waveBuffer[i];
                float v2 = vitals.waveBuffer[i + 1];
                
                float screenX1 = waveRect.x + i * scaleX;
                float screenY1 = centerY - v1 * (waveRect.height * 0.42f);
                float screenX2 = waveRect.x + (i + 1) * scaleX;
                float screenY2 = centerY - v2 * (waveRect.height * 0.42f);
                
                Vector2 pt1 = new Vector2(screenX1, screenY1);
                Vector2 pt2 = new Vector2(screenX2, screenY2);
                
                Vector2 dir = (pt2 - pt1).normalized;
                pt1 -= dir * 0.1f;
                pt2 += dir * 0.6f;
                
                Color drawGlow = glowColor * new Color(1f, 1f, 1f, alpha);
                Color drawCore = coreColor * new Color(1f, 1f, 1f, alpha);
                
                Widgets.DrawLine(pt1, pt2, drawGlow, 2.5f);
                Widgets.DrawLine(pt1, pt2, drawCore, 1.2f);
            }

            // --- 绘制右侧数值面板与精美排版 ---
            Rect rightPanel = new Rect(innerScreen.xMax - 48f, innerScreen.y, 48f, innerScreen.height);
            TextAnchor origAnchor = Text.Anchor;

            // 左上角 ECG 与 心跳闪烁图标
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.5f, 0.7f, 0.8f, 0.8f);
            Widgets.Label(new Rect(innerScreen.x + 4f, innerScreen.y + 2f, 30f, 15f), "ECG");
            
            bool isBlinking = bpm > EE_Constants.EcgFlatlineThreshold && (vitals.phase < 0.15f);
            GUI.color = isBlinking ? coreColor : coreColor * new Color(1f, 1f, 1f, 0.2f);
            Widgets.Label(new Rect(innerScreen.x + 28f, innerScreen.y + 2f, 15f, 15f), "♥");

            // HR (心率区域)
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = new Color(0.5f, 0.7f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 2f, rightPanel.width - 2f, 15f), "<b>HR</b>");

            Text.Font = GameFont.Medium;
            GUI.color = coreColor;
            string bpmStr = Mathf.RoundToInt(bpm).ToString();
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 12f, rightPanel.width - 2f, 26f), "<b>" + bpmStr + "</b>");

            // SpO2 (血氧区域)
            Color spo2Color = new Color(0.25f, 0.68f, 0.82f); // Cerulean
            if (spo2 < EE_Constants.EcgHypoxiaSpO2Threshold) spo2Color = colorCrimson;
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.7f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 38f, rightPanel.width - 2f, 15f), "SpO2");

            Text.Font = GameFont.Small;
            GUI.color = spo2Color;
            string spo2Str = (vitals.displaySpO2 < EE_Constants.SpO2MeasurableThreshold) ? "--" : spo2.ToString() + "%";
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 50f, rightPanel.width - 2f, 20f), spo2Str);

            // 还原状态
            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(rect))
            {
                return new GizmoResult(GizmoState.Interacted);
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }

    // Note: The GetGizmos Harmony patch has been consolidated into Patch_Pawn_GetGizmos.cs
}
