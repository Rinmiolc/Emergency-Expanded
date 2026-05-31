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
        public bool hasVentricularFibrillation = false;
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

            // 1. 根据生理年龄计算基准静息心率 (年轻略快，老年略慢)
            float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;
            float baseHR = 80f - Mathf.Clamp(bioAge - 15f, 0f, 60f) * 0.2f; // 人类范围在 68 - 80 左右

            // 2. 肾上腺素加成 (额外增加心率)
            Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost);
            if (adrenaline != null)
            {
                baseHR += adrenaline.Severity * 35f; // 最高 +35 bpm
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
            Hediff vFib = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.VentricularFibrillation);
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
                vitals.hasVentricularFibrillation = pawn.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation);
                
                if (vitals.heartRate > EE_Constants.EcgFlatlineThreshold)
                {
                    // 添加 ±2 bpm 的正常呼吸性心律不齐波动
                    vitals.displayHeartRate = vitals.heartRate + Rand.Range(-2f, 2f);
                    vitals.displayHeartRate = Mathf.Clamp(vitals.displayHeartRate, 10f, 280f);

                    // --- 高拟真电生理 SpO2 血氧饱和度算法 ---
                    float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                    float breathingDeficit = Mathf.Clamp01(1f - breathing);

                    float acidosisSeverity = 0f;
                    Hediff acidosis = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
                    if (acidosis != null)
                    {
                        acidosisSeverity = acidosis.Severity;
                    }

                    // 健康时血氧并不是定死 100% 而是处于 96% - 99% 这一现实黄金生理健康区间进行微微波动
                    float baseSpO2 = 97.8f + Rand.Range(-1.2f, 1.2f);
                    
                    // 病理惩罚：代谢性酸中毒代表全身微循环低灌注与严重组织乏氧，直接拖累血氧饱和度
                    baseSpO2 -= acidosisSeverity * 32f; // 重度酸中毒时，直接拖累血氧跌去 32%
                    
                    // 呼吸功能缺陷惩罚：如肺部受创直接导致携氧量雪崩
                    baseSpO2 -= breathingDeficit * 35f;

                    vitals.displaySpO2 = Mathf.Clamp(baseSpO2, 45f, 99.4f);
                }
                else
                {
                    vitals.displayHeartRate = 0f;
                    // 心跳骤停时，指夹式血氧仪测不到波形数据
                    // 但为了游戏性和拟真，让血氧逐步下降而不是瞬间归零
                    vitals.displaySpO2 -= 0.5f; 
                    if (vitals.displaySpO2 < 0f) vitals.displaySpO2 = 0f;
                }
            }
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

            if (bpm < EE_Constants.EcgFlatlineThreshold)
            {
                gridColor = new Color(0.4f, 0.0f, 0.0f, 0.15f);
                coreColor = new Color(1.0f, 0.15f, 0.15f, 1.0f);
                glowColor = new Color(1.0f, 0.0f, 0.0f, 0.4f);
            }
            else if (bpm > EE_Constants.EcgTachycardiaThreshold || bpm < EE_Constants.EcgBradycardiaThreshold || vitals.hasCerebralHypoxia || vitals.hasMetabolicAcidosis)
            {
                gridColor = new Color(0.4f, 0.2f, 0.0f, 0.15f);
                coreColor = new Color(1.0f, 0.7f, 0.1f, 1.0f);
                glowColor = new Color(1.0f, 0.5f, 0.0f, 0.4f);
            }
            else
            {
                gridColor = new Color(0f, 0.4f, 0.4f, 0.15f);
                coreColor = new Color(0.2f, 1.0f, 0.8f, 1.0f);
                glowColor = new Color(0.0f, 0.8f, 0.6f, 0.4f);
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

            float t = Time.realtimeSinceStartup;
            if (vitals.lastTime < 0f) vitals.lastTime = t;
            float dt = t - vitals.lastTime;
            
            if (Event.current.type == EventType.Repaint)
            {
                vitals.lastTime = t;
                if (dt > 0.1f) dt = 0.1f;

                float beatDuration = (bpm > EE_Constants.EcgFlatlineThreshold) ? (60f / bpm) : 1f;
                float sweepSpeed = waveWidth / 2.4f;
                
                int oldX = Mathf.FloorToInt(vitals.sweepX);
                vitals.sweepX += dt * sweepSpeed;
                if (vitals.sweepX >= waveWidth) vitals.sweepX -= waveWidth;
                int newX = Mathf.FloorToInt(vitals.sweepX);

                float phaseDelta = (bpm > EE_Constants.EcgFlatlineThreshold) ? (dt / beatDuration) : 0f;
                float newPhase = vitals.phase + phaseDelta;

                int stepPixels = newX - oldX;
                if (stepPixels < 0) stepPixels += Mathf.FloorToInt(waveWidth);

                if (stepPixels > 0)
                {
                    for (int step = 1; step <= stepPixels; step++)
                    {
                        int x = (oldX + step) % Mathf.FloorToInt(waveWidth);
                        float fraction = (float)step / stepPixels;
                        float p = (vitals.phase + phaseDelta * fraction) % 1f;
                        
                        float val = 0f;
                        float timeAtPixel = t - dt * (1f - fraction);

                        if (vitals.lastShockTime > 0f && timeAtPixel >= vitals.lastShockTime && timeAtPixel < vitals.lastShockTime + 0.6f)
                        {
                            // 电复律波峰模拟
                            float shockDt = timeAtPixel - vitals.lastShockTime;
                            if (shockDt < 0.05f) val = (shockDt / 0.05f) * 2.5f;
                            else if (shockDt < 0.15f) val = 2.5f - ((shockDt - 0.05f) / 0.1f) * 4.5f;
                            else if (shockDt < 0.35f) val = -2.0f + ((shockDt - 0.15f) / 0.2f) * 2.0f;
                            else val = Mathf.Sin((shockDt - 0.35f) * 15f) * 0.1f * (0.6f - shockDt);
                            
                            // 限制最大振幅，防止溢出 GUI 界面
                            val = Mathf.Clamp(val, -1.15f, 1.15f);
                        }
                        else if (bpm < EE_Constants.EcgFlatlineThreshold)
                        {
                            val = Mathf.Sin(timeAtPixel * 1.8f) * 0.024f + Rand.Range(-0.02f, 0.02f);
                        }
                        else if (vitals.hasVentricularFibrillation && bpm > 180f)
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
                            val = GetBaseECGValue(p, isHypoxic, bpm);
                            
                            // 反走样峰值捕捉：如果在这两帧相位之间跨越了波峰或波谷，强制该像素显示极限值
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

            // 绘制波形线段 (亚像素平滑绘制，弃用 FloorToInt)
            int drawPoints = Mathf.FloorToInt(waveWidth);
            for (int i = 0; i < drawPoints; i++)
            {
                float distToSweep = i - vitals.sweepX;
                if (distToSweep < 0f) distToSweep += waveWidth;
                
                float alpha = 1f;
                // 拉长尾部的阴影渐隐区，视觉更柔和
                if (distToSweep < 14f)
                {
                    alpha = distToSweep / 14f; 
                }
                
                if (i + 1 > vitals.sweepX && i <= vitals.sweepX) continue;
                
                float v1 = vitals.waveBuffer[i];
                float v2 = vitals.waveBuffer[i + 1];
                
                float screenX1 = waveRect.x + i;
                float screenY1 = centerY - v1 * (waveRect.height * 0.42f);
                float screenX2 = waveRect.x + i + 1;
                float screenY2 = centerY - v2 * (waveRect.height * 0.42f);
                
                Vector2 pt1 = new Vector2(screenX1, screenY1);
                Vector2 pt2 = new Vector2(screenX2, screenY2);
                
                // 延长线段以使其首尾互相重叠，消除像素渲染产生的虚线间隙现象
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
            GUI.color = new Color(0.5f, 0.6f, 0.5f, 0.7f);
            Widgets.Label(new Rect(innerScreen.x + 4f, innerScreen.y + 2f, 30f, 15f), "ECG");
            
            bool isBlinking = bpm > EE_Constants.EcgFlatlineThreshold && (vitals.phase < 0.15f);
            GUI.color = isBlinking ? coreColor : coreColor * new Color(1f, 1f, 1f, 0.2f);
            Widgets.Label(new Rect(innerScreen.x + 28f, innerScreen.y + 2f, 15f, 15f), "♥");

            // HR (心率区域)
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = coreColor * new Color(1f, 1f, 1f, 0.75f);
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 2f, rightPanel.width - 2f, 15f), "HR");

            Text.Font = GameFont.Medium;
            GUI.color = coreColor;
            string bpmStr = Mathf.RoundToInt(bpm).ToString();
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 12f, rightPanel.width - 2f, 26f), bpmStr);

            // SpO2 (血氧区域)
            Color spo2Color = new Color(0.2f, 0.8f, 1.0f);
            if (spo2 < EE_Constants.EcgHypoxiaSpO2Threshold) spo2Color = new Color(1.0f, 0.3f, 0.3f);
            
            Text.Font = GameFont.Tiny;
            GUI.color = spo2Color * new Color(1f, 1f, 1f, 0.75f);
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 38f, rightPanel.width - 2f, 15f), "SpO2");

            Text.Font = GameFont.Small;
            GUI.color = spo2Color;
            string spo2Str = spo2.ToString() + "%";
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

        // 极度还原临床心电图 Lead-II 波形函数 (P-QRS-T)
        // 极度还原临床心电图 Lead-II 波形函数 (P-QRS-T)
        // 包含心肌缺血/缺氧时的 ST段压低 与 T波倒置 拟真病理改变
        private float GetBaseECGValue(float p, bool isHypoxic, float bpm)
        {
            float beatDuration = (bpm > 0.1f) ? (60f / bpm) : 1f;
            float activeDuration = 0.45f;
            // 限制活跃部分比例，确保波形不会在极高心率下互相重叠超过界限
            float activeFraction = Mathf.Clamp(activeDuration / beatDuration, 0.1f, 0.95f);
            
            if (p > activeFraction) return 0f; // TP段基线
            
            // 将 p 映射到标准的 0~0.55 区间，保证 P-QRS-T 波形在物理时间（屏幕宽度）上的一致性
            float origP = (p / activeFraction) * 0.55f;
            
            if (origP < 0.05f) return 0f;
            // P波 (心房除极)
            if (origP < 0.13f) 
                return Mathf.Sin(((origP - 0.05f) / 0.08f) * Mathf.PI) * 0.12f;
            // PR段
            if (origP < 0.18f) 
                return 0f;
            // Q波 (室间隔除极)
            if (origP < 0.20f) 
                return -((origP - 0.18f) / 0.02f) * 0.15f;
            // R波 (心室主除极 - 极速上升)
            if (origP < 0.23f) 
                return -0.15f + ((origP - 0.20f) / 0.03f) * 1.35f; // R波顶峰 1.20
            // S波 (心室基底除极 - 极速下降)
            if (origP < 0.26f) 
                return 1.20f - ((origP - 0.23f) / 0.03f) * 1.55f; // S波谷底 -0.35
            
            // ST段 (缓慢回基线)
            if (origP < 0.32f) 
            {
                float stBase = -0.35f + ((origP - 0.26f) / 0.06f) * 0.35f;
                // 脑部窒息缺氧或重度低血容量休克时：发生 ST段压低 (ST Depression) 0.08 像素单位
                return isHypoxic ? (stBase - 0.08f) : stBase;
            }
            
            // T波 (心室复极 - 不对称宽波)
            if (origP < 0.55f) 
            {
                float tPhase = (origP - 0.32f) / 0.23f;
                float tWave = Mathf.Pow(Mathf.Sin(tPhase * Mathf.PI), 1.5f) * 0.28f;
                if (isHypoxic)
                {
                    // 缺氧病理：T波对称性倒置 (T Wave Inversion)
                    return -tWave * 0.8f - 0.05f; 
                }
                return tWave;
            }
            return 0f;
        }
    }

    // ================= 4. Harmony 注入与过滤挂载补丁 =================
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // 健壮性与兼容性过滤
            if (__instance == null || __instance.Dead) return;
            if (!__instance.RaceProps.IsFlesh) return; // 排除机械族、石头人等非肉体
            if (__instance.IsShambler) return;        // 排除 1.5/1.6 异常蹒跚怪
            if (__instance.RaceProps.IsMechanoid) return; // 极佳的安全过滤排除机械族
            if (!EE_Settings.EnableEcgGui) return;    // 检查设置开关

            // 仅对玩家当前选中操控的单个血肉生物显示心电图体征仪（防止多选时UI过载）
            if (Find.Selector.SingleSelectedThing == __instance)
            {
                __result = AddMonitorGizmo(__instance, __result);
            }
        }

        private static IEnumerable<Gizmo> AddMonitorGizmo(Pawn pawn, IEnumerable<Gizmo> originalGizmos)
        {
            foreach (var gizmo in originalGizmos)
            {
                yield return gizmo;
            }
            yield return new Gizmo_VitalMonitor(pawn);
        }
    }
}
