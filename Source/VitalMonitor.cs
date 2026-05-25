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
                
                if (vitals.heartRate > 0.1f)
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
                    vitals.displaySpO2 = 0f; // 心跳骤停时，指夹式血氧仪测不到波形数据
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

        public override float GetWidth(float maxWidth) => 160f; // 黄金比例加长型检测仪

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            // 定义 Gizmo 面板的整块区域 (160 x 75 像素)
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            
            // 【UI对齐核心修复】：显式绘制全尺寸 160x75 的原版切角面板背景。
            // 这将彻底消除底层由于引擎默认 Command 引起的 75x75 灰色方块残留，确保与旁边所有按钮在高度与外观上完美对齐。
            Widgets.DrawWindowBackground(rect);
            
            // 获取与更新生理缓存
            CachedVitals vitals = VitalTracker.GetOrCreateVitals(pawn);
            if (vitals == null) return new GizmoResult(GizmoState.Clear);

            VitalTracker.UpdateVitalsIfNeed(pawn, vitals);
            float bpm = vitals.displayHeartRate;

            // 获取 SpO2 血氧饱和度
            int spo2 = Mathf.RoundToInt(vitals.displaySpO2);

            // 1. 动态决定颜色主题 (医疗青/黄/红)
            Color gridColor;
            Color coreColor;
            Color glowColor;

            if (bpm < 0.1f) // 心跳骤停
            {
                gridColor = new Color(0.4f, 0.0f, 0.0f, 0.15f);    // 红色警戒背景网格
                coreColor = new Color(1.0f, 0.15f, 0.15f, 1.0f);     // 荧光红折线核心
                glowColor = new Color(1.0f, 0.0f, 0.0f, 0.4f);       // 红色外发光
            }
            else if (bpm > 140f || bpm < 45f || pawn.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia) || pawn.health.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis))
            {
                gridColor = new Color(0.4f, 0.2f, 0.0f, 0.15f);      // 橙黄警戒背景网格
                coreColor = new Color(1.0f, 0.7f, 0.1f, 1.0f);       // 荧光橙黄折线核心
                glowColor = new Color(1.0f, 0.5f, 0.0f, 0.4f);       // 橙黄外发光
            }
            else
            {
                gridColor = new Color(0f, 0.4f, 0.4f, 0.15f);        // 医疗青绿网格
                coreColor = new Color(0.2f, 1.0f, 0.8f, 1.0f);       // 荧光青绿折线核心
                glowColor = new Color(0.0f, 0.8f, 0.6f, 0.4f);       // 青绿外发光
            }

            // --- 绘制高级质感背景 ---
            // 【UI重塑】：彻底删除硬直角的自定义外框 DrawBoxSolid，直接利用原版斜切角 Gizmo 背景作为物理外壳！
            // 黑色屏幕向内稍微缩进 4 像素，以形成完美的一体化“嵌入式液晶屏”视觉效果。
            Rect innerScreen = rect.ContractedBy(4f);
            // 纯黑深邃液晶屏幕
            Widgets.DrawBoxSolid(innerScreen, new Color(0.02f, 0.02f, 0.025f, 1f));
            
            // 顶部玻璃微反光质感
            Rect glassRect = innerScreen;
            glassRect.height = innerScreen.height * 0.35f;
            Widgets.DrawBoxSolid(glassRect, new Color(1f, 1f, 1f, 0.025f));

            // 绘制网格
            GUI.color = gridColor;
            for (int i = 0; i <= 4; i++)
            {
                float y = innerScreen.y + (innerScreen.height / 4f) * i;
                Widgets.DrawLine(new Vector2(innerScreen.x, y), new Vector2(innerScreen.xMax, y), GUI.color, 1f);
            }
            for (int i = 0; i <= 8; i++)
            {
                float x = innerScreen.x + (innerScreen.width / 8f) * i;
                Widgets.DrawLine(new Vector2(x, innerScreen.y), new Vector2(x, innerScreen.yMax), GUI.color, 1f);
            }
            GUI.color = Color.white;

            // --- 从左至右扫描的波形渲染逻辑 (环形缓冲区防止抽动) ---
            Rect waveRect = new Rect(innerScreen.x + 2f, innerScreen.y + 20f, 105f, 46f);
            float centerY = waveRect.y + waveRect.height / 2f;
            float waveWidth = waveRect.width;

            float t = Time.realtimeSinceStartup;
            if (vitals.lastTime < 0f) vitals.lastTime = t;
            float dt = t - vitals.lastTime;
            
            // 核心修复：仅在 Repaint 事件中更新生理波形状态与时间步长，杜绝 IMGUI 多重调用（Layout/Repaint 等）造成的波形抽动与抖动
            if (Event.current.type == EventType.Repaint)
            {
                vitals.lastTime = t;
                if (dt > 0.1f) dt = 0.1f; // 限制最大步长防卡顿跳跃

                float beatDuration = (bpm > 0.1f) ? (60f / bpm) : 1f;
                float sweepSpeed = waveWidth / 2.4f; // 2.4秒扫满一屏
                
                int oldX = Mathf.FloorToInt(vitals.sweepX);
                vitals.sweepX += dt * sweepSpeed;
                if (vitals.sweepX >= waveWidth) vitals.sweepX -= waveWidth;
                int newX = Mathf.FloorToInt(vitals.sweepX);

                float phaseDelta = (bpm > 0.1f) ? (dt / beatDuration) : 0f;
                float newPhase = vitals.phase + phaseDelta;

                int stepPixels = newX - oldX;
                if (stepPixels < 0) stepPixels += Mathf.FloorToInt(waveWidth);

                // 补全这一帧扫过的像素点波形
                if (stepPixels > 0)
                {
                    for (int step = 1; step <= stepPixels; step++)
                    {
                        int x = (oldX + step) % Mathf.FloorToInt(waveWidth);
                        float fraction = (float)step / stepPixels;
                        float p = (vitals.phase + phaseDelta * fraction) % 1f;
                        
                        float val = 0f;
                        if (bpm < 0.1f)
                        {
                            // 心跳骤停 (Flatline) 微弱起伏：包含极其缓慢的基线游走波动 (胸腔起伏/残余电信号) 与电磁干扰噪声抖动
                            float timeAtPixel = t - dt * (1f - fraction);
                            val = Mathf.Sin(timeAtPixel * 1.8f) * 0.024f + Rand.Range(-0.02f, 0.02f);
                        }
                        else if (pawn.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation) && bpm > 180f)
                        {
                            // 【高拟真室颤波重塑】：融合质数频率谐波与缓慢的低频调制，塑造出极度无规则、波幅宽窄动态漂移的混沌乱颤蠕动波
                            float timeAtPixel = t - dt * (1f - fraction);
                            float amplitudeMod = 0.75f + Mathf.Sin(timeAtPixel * 7.5f) * 0.25f; // 7.5Hz低频振幅漂移
                            float baseWave = Mathf.Sin(timeAtPixel * 37f) * 0.32f + 
                                             Mathf.Cos(timeAtPixel * 79f) * 0.18f + 
                                             Mathf.Sin(timeAtPixel * 131f) * 0.10f;
                            float noise = Rand.Range(-0.08f, 0.08f); // 高频微颤抖动
                            val = baseWave * amplitudeMod + noise;
                        }
                        else
                        {
                            // 缺氧与心肌严重缺血病理判定
                            bool isHypoxic = pawn.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia) || bpm > 140f;
                            val = GetBaseECGValue(p, isHypoxic);
                        }
                        if (x >= 0 && x < vitals.waveBuffer.Length)
                            vitals.waveBuffer[x] = val;
                    }
                }
                vitals.phase = newPhase % 1f;
            }

            // 绘制波形线段
            for (int i = 0; i < Mathf.FloorToInt(waveWidth) - 1; i++)
            {
                // 计算该点到扫描头的顺时针距离（即扫描头前方的距离）
                float distToSweep = i - vitals.sweepX;
                if (distToSweep < 0f) distToSweep += waveWidth;
                
                // 扫描头前方 8 像素内逐渐淡出到 0，完美实现扫描阴影断层，杜绝硬剔除带来的边缘毛糙与视觉断点
                float alpha = 1f;
                if (distToSweep < 8f)
                {
                    alpha = distToSweep / 8f; // 0 到 1 线性渐变
                }
                
                // 跨越扫描线时不连接，防止首尾两端产生突变的粘连折线
                if (i + 1 > vitals.sweepX && i < vitals.sweepX) continue;
                
                float v1 = vitals.waveBuffer[i];
                float v2 = vitals.waveBuffer[i + 1];
                
                float screenX1 = waveRect.x + i;
                float screenY1 = centerY - v1 * (waveRect.height * 0.42f);
                float screenX2 = waveRect.x + i + 1;
                float screenY2 = centerY - v2 * (waveRect.height * 0.42f);
                
                Color drawGlow = glowColor * new Color(1f, 1f, 1f, alpha);
                Color drawCore = coreColor * new Color(1f, 1f, 1f, alpha);
                
                // 外辉光 (带 Alpha 渐变)
                Widgets.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), drawGlow, 2.5f);
                // 亮芯 (带 Alpha 渐变)
                Widgets.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), drawCore, 1.2f);
            }

            // 绘制扫描头亮线
            float headX = waveRect.x + vitals.sweepX;
            Widgets.DrawLine(new Vector2(headX, innerScreen.y + 2f), new Vector2(headX, innerScreen.yMax - 2f), coreColor * new Color(1f, 1f, 1f, 0.15f), 1f);


            // --- 绘制右侧数值面板与精美排版 ---
            Rect rightPanel = new Rect(innerScreen.xMax - 46f, innerScreen.y, 46f, innerScreen.height);
            TextAnchor origAnchor = Text.Anchor;

            // ECG 标识
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.5f, 0.6f, 0.5f, 0.7f);
            Widgets.Label(new Rect(innerScreen.x + 18f, innerScreen.y + 2f, 50f, 15f), "ECG");

            // 跳动的心脏图标 ♥
            bool isBlinking = bpm > 0.1f && (vitals.phase < 0.15f);
            GUI.color = isBlinking ? coreColor : coreColor * new Color(1f, 1f, 1f, 0.2f);
            Widgets.Label(new Rect(innerScreen.x + 4f, innerScreen.y + 2f, 15f, 15f), "♥");

            // 心率数值 (大号字体，偏上居中，去掉英文标签)
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = coreColor;
            string bpmStr = Mathf.RoundToInt(bpm).ToString(); // 骤停时直接显示数字 0，符合现实
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 10f, rightPanel.width - 2f, 26f), bpmStr);

            // 血氧数值 (中号字体，偏下居中，去掉英文标签)
            Color spo2Color = new Color(0.2f, 0.8f, 1.0f); // 医疗蓝
            if (spo2 < 90) spo2Color = new Color(1.0f, 0.3f, 0.3f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = spo2Color;
            string spo2Str = spo2.ToString() + "%"; // 骤停时直接显示数字 0%，符合现实
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 42f, rightPanel.width - 2f, 18f), spo2Str);

            // 还原状态
            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 4. 监听左键点击 (精简无干扰)
            if (Widgets.ButtonInvisible(rect))
            {
                return new GizmoResult(GizmoState.Interacted);
            }

            return new GizmoResult(GizmoState.Clear);
        }

        // 极度还原临床心电图 Lead-II 波形函数 (P-QRS-T)
        // 极度还原临床心电图 Lead-II 波形函数 (P-QRS-T)
        // 包含心肌缺血/缺氧时的 ST段压低 与 T波倒置 拟真病理改变
        private float GetBaseECGValue(float p, bool isHypoxic)
        {
            if (p < 0.05f) return 0f;
            // P波 (心房除极)
            if (p < 0.13f) 
                return Mathf.Sin(((p - 0.05f) / 0.08f) * Mathf.PI) * 0.12f;
            // PR段
            if (p < 0.18f) 
                return 0f;
            // Q波 (室间隔除极)
            if (p < 0.20f) 
                return -((p - 0.18f) / 0.02f) * 0.15f;
            // R波 (心室主除极 - 极速上升)
            if (p < 0.23f) 
                return -0.15f + ((p - 0.20f) / 0.03f) * 1.35f; // R波顶峰 1.20
            // S波 (心室基底除极 - 极速下降)
            if (p < 0.26f) 
                return 1.20f - ((p - 0.23f) / 0.03f) * 1.55f; // S波谷底 -0.35
            
            // ST段 (缓慢回基线)
            if (p < 0.32f) 
            {
                float stBase = -0.35f + ((p - 0.26f) / 0.06f) * 0.35f;
                // 脑部窒息缺氧或重度低血容量休克时：发生 ST段压低 (ST Depression) 0.08 像素单位
                return isHypoxic ? (stBase - 0.08f) : stBase;
            }
            
            // T波 (心室复极 - 不对称宽波)
            if (p < 0.55f) 
            {
                float tPhase = (p - 0.32f) / 0.23f;
                float tWave = Mathf.Pow(Mathf.Sin(tPhase * Mathf.PI), 1.5f) * 0.28f;
                if (isHypoxic)
                {
                    // 缺氧病理：T波对称性倒置 (T Wave Inversion)
                    return -tWave * 0.8f - 0.05f; 
                }
                return tWave;
            }
            return 0f; // TP段基线
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
