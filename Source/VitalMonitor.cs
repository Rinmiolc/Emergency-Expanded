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

            // 3. 失血状态加成
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
                    baseHR = Mathf.Lerp(150f, 30f, (severity - 0.8f) / 0.2f);
                }
            }

            // 4. 心律失常与骤停状态判定
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
            // 外框边角
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.09f, 1f));
            Rect innerScreen = rect.ContractedBy(3f);
            // 纯黑深邃屏幕
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
                        if (bpm < 0.1f) val = 0f;
                        else if (pawn.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation) && bpm > 180f)
                        {
                            float timeAtPixel = t - dt * (1f - fraction);
                            val = Mathf.Sin(timeAtPixel * 35f) * 0.3f + Mathf.Sin(timeAtPixel * 70f) * 0.15f + Rand.Range(-0.05f, 0.05f);
                        }
                        else
                        {
                            val = GetBaseECGValue(p);
                        }
                        if (x >= 0 && x < vitals.waveBuffer.Length)
                            vitals.waveBuffer[x] = val;
                    }
                }
                vitals.phase = newPhase % 1f;
            }

            // 绘制波形线段
            int gapWidth = 5;
            for (int i = 0; i < Mathf.FloorToInt(waveWidth) - 1; i++)
            {
                float distToSweep = vitals.sweepX - i;
                if (distToSweep < 0) distToSweep += waveWidth;
                if (distToSweep >= 0 && distToSweep <= gapWidth) continue; // 留出扫描头前方的断层空隙
                
                // 跨越扫描线时不连接
                if (i + 1 > vitals.sweepX && i < vitals.sweepX) continue;
                
                float v1 = vitals.waveBuffer[i];
                float v2 = vitals.waveBuffer[i + 1];
                
                float screenX1 = waveRect.x + i;
                float screenY1 = centerY - v1 * (waveRect.height * 0.42f);
                float screenX2 = waveRect.x + i + 1;
                float screenY2 = centerY - v2 * (waveRect.height * 0.42f);
                
                // 外辉光
                Widgets.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), glowColor, 2.5f);
                // 亮芯
                Widgets.DrawLine(new Vector2(screenX1, screenY1), new Vector2(screenX2, screenY2), coreColor, 1.2f);
            }

            // 绘制扫描头亮线
            float headX = waveRect.x + vitals.sweepX;
            Widgets.DrawLine(new Vector2(headX, innerScreen.y + 2f), new Vector2(headX, innerScreen.yMax - 2f), coreColor * new Color(1f, 1f, 1f, 0.15f), 1f);


            // --- 绘制右侧数值面板与精美排版 ---
            Rect rightPanel = new Rect(innerScreen.xMax - 46f, innerScreen.y, 46f, innerScreen.height);
            TextAnchor origAnchor = Text.Anchor;

            // LEAD II 标识
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.5f, 0.6f, 0.5f, 0.7f);
            Widgets.Label(new Rect(innerScreen.x + 18f, innerScreen.y + 2f, 50f, 15f), "LEAD II");

            // 跳动的心脏图标 ♥
            bool isBlinking = bpm > 0.1f && (vitals.phase < 0.15f);
            GUI.color = isBlinking ? coreColor : coreColor * new Color(1f, 1f, 1f, 0.2f);
            Widgets.Label(new Rect(innerScreen.x + 4f, innerScreen.y + 2f, 15f, 15f), "♥");

            // 心率数值 (大号字体，偏上居中，去掉英文标签)
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = coreColor;
            string bpmStr = (bpm < 0.1f) ? "---" : Mathf.RoundToInt(bpm).ToString();
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 10f, rightPanel.width - 2f, 26f), bpmStr);

            // 血氧数值 (中号字体，偏下居中，去掉英文标签)
            Color spo2Color = new Color(0.2f, 0.8f, 1.0f); // 医疗蓝
            if (spo2 < 90) spo2Color = new Color(1.0f, 0.3f, 0.3f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = spo2Color;
            string spo2Str = (bpm < 0.1f) ? "--" : spo2.ToString() + "%";
            Widgets.Label(new Rect(rightPanel.x, rightPanel.y + 42f, rightPanel.width - 2f, 18f), spo2Str);

            // 还原状态
            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 4. 注册悬停 Tip (新手引导)
            string diagnosisTip = GetMedicalDiagnosisAndTutorial(pawn, vitals);
            TooltipHandler.TipRegion(rect, diagnosisTip);

            // 5. 监听左键点击
            if (Widgets.ButtonInvisible(rect))
            {
                Messages.Message($"{pawn.NameShortColored} 生命体征监视中，悬停鼠标可查看详细医学诊断。", MessageTypeDefOf.NeutralEvent, false);
                return new GizmoResult(GizmoState.Interacted);
            }

            return new GizmoResult(GizmoState.Clear);
        }

        // 极度还原临床心电图 Lead-II 波形函数 (P-QRS-T)
        private float GetBaseECGValue(float p)
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
                return -0.35f + ((p - 0.26f) / 0.06f) * 0.35f;
            // T波 (心室复极 - 不对称宽波)
            if (p < 0.55f) 
            {
                float tPhase = (p - 0.32f) / 0.23f;
                // 用 Pow(sin, 1.5) 塑造 T波不对称感
                return Mathf.Pow(Mathf.Sin(tPhase * Mathf.PI), 1.5f) * 0.28f;
            }
            return 0f; // TP段基线
        }

        private string GetMedicalDiagnosisAndTutorial(Pawn p, CachedVitals v)
        {
            if (p == null || p.Dead || v == null) return "无法获取体征数据。";

            float pumping = p.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = p.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float bleedRate = p.health.hediffSet.BleedRateTotal;
            int bpm = Mathf.RoundToInt(v.displayHeartRate);

            string text = $"<b>【生命体征监测仪 - {p.NameShortColored}】</b>\n";
            text += "---------------------------------\n";
            text += $"<b>实时心率</b>: {((bpm == 0) ? "<color=red>0次/分 (骤停!)</color>" : $"{bpm}次/分")}\n";
            text += $"<b>全身泵血</b>: {pumping.ToStringPercent()}\n";
            text += $"<b>全身呼吸</b>: {breathing.ToStringPercent()}\n";
            text += $"<b>总流血率</b>: {(bleedRate > 0.01f ? $"<color=red>{bleedRate:F1}/天</color>" : "无流血")}\n";
            text += "---------------------------------\n";
            text += "<b>【实时医学诊断与急救指引】</b>\n";

            // 1. 心脏骤停判定
            Hediff vFeb = p.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.VentricularFibrillation);
            if (vFeb != null)
            {
                if (vFeb.Severity >= 0.60f)
                {
                    text += "<color=red>⚠️【致命！心跳骤停 (Flatline)】</color>\n";
                    text += "原因: 心室颤动崩溃或严重失氧导致心脏停止跳动，几分钟内将脑死亡！\n";
                    text += "急救措施: <color=yellow>请立刻派人对伤者实施【心肺复苏术 (CPR)】以维持人工脑供氧，并配合【除颤抢救】！</color>";
                    return text;
                }
                else
                {
                    text += "<color=red>⚠️【致命！心室颤动 (V-Fib)】</color>\n";
                    text += "原因: 严重酸中毒或物理电击导致心肌纤维乱颤，已失去泵血功能！\n";
                    text += "急救措施: <color=yellow>小人将在数秒内昏迷并骤停。必须立刻使用【自动体外除颤器 (AED)】进行除颤！</color>";
                    return text;
                }
            }

            // 2. 严重流血与休克代偿判定
            Hediff bloodLoss = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                if (bloodLoss.Severity >= 0.80f)
                {
                    text += "<color=red>⚠️【危急！终末代偿失调休克】</color>\n";
                    text += "原因: 全身失血已达致命极限。心肌供氧严重崩溃导致心率慢阻骤跌，濒临骤停！\n";
                    text += "急救措施: <color=yellow>必须在几秒钟内完成止血包扎，并火速通过【静脉补液/输血】抢救！</color>\n";
                }
                else if (bloodLoss.Severity >= 0.30f)
                {
                    text += "<color=yellow>⚠️【危重！低血容量性代偿过速】</color>\n";
                    text += "原因: 全身血管容量急剧丢失，心脏被迫高频超载代偿以维持脑血压。\n";
                    text += "急救措施: <color=yellow>请迅速使用止血带包扎主要伤口，防止代偿崩溃引发酸中毒休克！</color>\n";
                }
            }

            // 3. 脑部窒息危机判定
            Hediff brainHypoxia = p.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia);
            if (brainHypoxia != null)
            {
                text += "<color=red>⚠️【危急！脑组织急性缺氧】</color>\n";
                text += $"脑缺氧严重度: {(brainHypoxia.Severity * 100f):F0}%\n";
                text += "原因: 泵血不足或呼吸受阻。缺氧达到60%以上将产生不可逆的【永久脑损伤】，到100%直接脑死亡陷入植物人！\n";
                text += "急救措施: <color=yellow>请火速包扎止血以恢复脑供血；若呼吸通道受阻，请确保清理呼吸道或戴上人工面罩！</color>\n";
            }

            // 4. 代谢性酸中毒判定
            Hediff acidosis = p.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
            if (acidosis != null && acidosis.Severity >= 0.50f)
            {
                text += "<color=red>⚠️【危重！全身代谢性酸中毒】</color>\n";
                text += "原因: 长时间低灌注与局部肢体坏死引发酸碱失衡大崩溃，晚期可直接诱发心搏骤停。\n";
                text += "急救措施: <color=yellow>必须尽快纠正失血低血压状态；若酸中毒危及心跳，请紧急注射【碳酸氢钠针剂】中和血液！</color>\n";
            }

            // 6. 全身健康
            if (brainHypoxia == null && bloodLoss == null && vFeb == null && acidosis == null)
            {
                text += "<color=green>✓【生命体征总体安全】</color>\n";
                text += "监测报告: 目前各项核心维生器官工作状态良好，微循环稳定，无急症危机风险。";
            }

            return text;
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
