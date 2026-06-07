using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace EmergencyExpanded
{
    public class ITab_Pawn_Health_EE : ITab_Pawn_Health
    {
        private bool IsNiceHealthTabActive
        {
            get
            {
                return ModLister.GetActiveModWithIdentifier("Andromeda.NiceHealthTab") != null;
            }
        }

        private bool IsHealthDisplayActive
        {
            get
            {
                return ModLister.GetActiveModWithIdentifier("GT.Sam.HealthDisplay") != null;
            }
        }

        private Vector2 OverhaulSize
        {
            get
            {
                return new Vector2(780f, 700f);
            }
        }

        private bool ShouldEnableOverhaul
        {
            get
            {
                return EE_Mod.Settings.enableHealthUiOverhaul;
            }
        }

        private Vector2 DefaultSize
        {
            get
            {
                return new Vector2(780f, 430f);
            }
        }

        public ITab_Pawn_Health_EE() : base()
        {
            this.size = this.ShouldEnableOverhaul ? this.OverhaulSize : this.DefaultSize;
            this.labelKey = "TabHealth"; // Keep standard "Health" tab label
        }

        protected Pawn PawnForHealthCard
        {
            get
            {
                if (this.SelPawn != null) return this.SelPawn;
                Corpse corpse = this.SelThing as Corpse;
                if (corpse != null) return corpse.InnerPawn;
                return null;
            }
        }

        protected bool AllowBlockBodyParts => true;

        protected bool ShowOperationsTab
        {
            get
            {
                Pawn pawn = this.PawnForHealthCard;
                if (pawn == null || pawn.Dead) return false;
                return pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || (pawn.Faction == null && pawn.RaceProps.Animal);
            }
        }

        protected override void UpdateSize()
        {
            base.UpdateSize();
            this.size = this.ShouldEnableOverhaul ? this.OverhaulSize : this.DefaultSize;
        }

        public override void OnOpen()
        {
            this.size = this.ShouldEnableOverhaul ? this.OverhaulSize : this.DefaultSize;
            base.OnOpen();
        }

        protected override void FillTab()
        {
            Pawn pawn = this.PawnForHealthCard;
            if (pawn == null) return;

            if (!this.ShouldEnableOverhaul)
            {
                base.FillTab();
                return;
            }

            Patch_HealthCardUtility_UI.isDrawingHealthTab = true;
            Patch_HealthCardUtility_UI.lastHealthTabDrawFrame = Time.frameCount;
            Patch_HealthCardUtility_UI.useDarkMode = true; // Always true if overhaul enabled
            try
            {
                float tabWidth = this.size.x;
                Rect topRect = new Rect(0f, 0f, tabWidth, 210f);

                if (pawn.RaceProps.IsFlesh && !pawn.IsShambler && !pawn.RaceProps.IsMechanoid)
                {
                    DrawMedicalMonitorPanel(topRect, pawn);
                }
                else
                {
                    TextAnchor origAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(topRect, "EE_NonFleshNoMonitor".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = origAnchor;
                }

                Rect dividerRect = new Rect(10f, 212f, tabWidth - 20f, 1f);
                Widgets.DrawBoxSolid(dividerRect, new Color(0.3f, 0.3f, 0.3f, 0.25f));

                Rect bottomRect = new Rect(0f, 220f, tabWidth, 480f);
                
                Rect fullBottomBg = new Rect(0f, 210f, tabWidth, 490f);
                Widgets.DrawBoxSolid(fullBottomBg, new Color(0.12f, 0.12f, 0.13f));

                try
                {
                    bool showBloodLoss = true;
                    if (this.IsNiceHealthTabActive)
                    {
                        this.SetNHTAllowed(true);
                    }
                    HealthCardUtility.DrawPawnHealthCard(bottomRect, pawn, this.ShowOperationsTab, showBloodLoss, this.SelThing);
                }
                catch (Exception ex)
                {
                    Log.Error("[EE] Error drawing pawn health card: " + ex.ToString());
                }
            }
            finally
            {
                Patch_HealthCardUtility_UI.isDrawingHealthTab = false;
            }
        }

        private static System.Reflection.FieldInfo nhtAllowedField = null;
        private static bool nhtFieldLookedUp = false;

        private void SetNHTAllowed(bool allowed)
        {
            if (!nhtFieldLookedUp)
            {
                nhtFieldLookedUp = true;
                try
                {
                    Type type = GenTypes.GetTypeInAnyAssembly("NiceHealthTab.Patches+DrawHediffListing_Patch");
                    if (type != null)
                    {
                        nhtAllowedField = type.GetField("Allowed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[EE] Failed to find NiceHealthTab Allowed field: " + ex.ToString());
                }
            }

            if (nhtAllowedField != null)
            {
                try
                {
                    nhtAllowedField.SetValue(null, allowed);
                }
                catch (Exception ex)
                {
                    Log.Error("[EE] Failed to set NiceHealthTab Allowed field: " + ex.ToString());
                }
            }
        }


        private void DrawMedicalMonitorPanel(Rect rect, Pawn pawn)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.13f, 0.95f)); // Modern dark background
            Rect inner = rect.ContractedBy(10f);

            CachedVitals vitals = VitalTracker.GetOrCreateVitals(pawn);
            if (vitals == null) return;

            VitalTracker.UpdateVitalsIfNeed(pawn, vitals);

            // 1. 左侧栏 (宽度 432f)：心电波形图在上，二级体征网格和动作按钮在下 (扩展五分之一/1.2倍)
            Rect leftTopRect = new Rect(inner.x, inner.y, 432f, 100f);
            DrawMonitorScreen(leftTopRect, pawn, vitals);

            Rect leftBottomRect = new Rect(inner.x, inner.y + 105f, 432f, 85f);
            Rect gridRect = new Rect(leftBottomRect.x, leftBottomRect.y, leftBottomRect.width, 42f);
            DrawSecondaryGrid(gridRect, pawn, vitals);

            Rect actionsRect = new Rect(leftBottomRect.x, leftBottomRect.y + 48f, leftBottomRect.width, 35f);
            DrawQuickActions(actionsRect, pawn);

            // 2. 右侧栏 (右移并自适应剩余宽度)：整合式体征+诊断面板
            Rect rightColumnRect = new Rect(inner.x + 442f, inner.y, inner.width - 442f, 190f);
            DrawRightMonitorPanel(rightColumnRect, pawn, vitals);
        }

        private void DrawSecondaryGrid(Rect rect, Pawn pawn, CachedVitals vitals)
        {
            float colWidth = rect.width / 2f - 4f;
            float rowHeight = rect.height / 2f;

            Rect tempRect = new Rect(rect.x, rect.y, colWidth, rowHeight);
            Rect phRect = new Rect(rect.x + colWidth + 8f, rect.y, colWidth, rowHeight);
            Rect volRect = new Rect(rect.x, rect.y + rowHeight, colWidth, rowHeight);
            Rect bleedRect = new Rect(rect.x + colWidth + 8f, rect.y + rowHeight, colWidth, rowHeight);

            // Calculate values
            float bloodVolume = 1f;
            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null) bloodVolume = Mathf.Clamp01(1f - bloodLoss.Severity);
            
            float maxBloodMl = 5000f * pawn.BodySize;
            float currentBloodMl = maxBloodMl * bloodVolume;
            float volPct = bloodVolume * 100f;

            // 读取缓存的体征数据 (2秒一结算)
            float ph = vitals.displaypH;
            float temp = vitals.displayTemp;
            
            void DrawGridItem(Rect r, string label, string val, Color valColor)
            {
                // Subtle underline
                Widgets.DrawLine(new Vector2(r.x, r.yMax - 2f), new Vector2(r.xMax, r.yMax - 2f), new Color(0.3f, 0.3f, 0.3f, 0.4f), 1f);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.5f, 0.7f, 0.8f);
                Widgets.Label(new Rect(r.x + 2f, r.y, 40f, r.height), label);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = valColor;
                Widgets.Label(new Rect(r.x + 45f, r.y, r.width - 47f, r.height), pawn.Dead ? "---" : val);
            }

            Color colorCrimson = new Color(0.83f, 0.25f, 0.25f);
            Color colorAmber = new Color(0.85f, 0.60f, 0.25f);
            Color colorMint = new Color(0.32f, 0.78f, 0.52f);

            Color tempColor = (temp < 35.0f || temp > 38.5f) ? colorCrimson : ((temp < 36.0f || temp > 37.5f) ? colorAmber : colorMint);
            DrawGridItem(tempRect, "Temp", temp.ToString("F1") + "°C", tempColor);

            Color phColor = (ph < 7.30f) ? colorCrimson : colorMint;
            DrawGridItem(phRect, "pH", ph.ToString("F2"), phColor);

            Color volColor = bloodLoss?.Severity > 0.4f ? colorCrimson : (bloodLoss?.Severity > 0.15f ? colorAmber : colorMint);
            DrawGridItem(volRect, "Vol", volPct.ToString("F0") + "% (" + currentBloodMl.ToString("F0") + "ml)", volColor);

            string bleedRateStr;
            Color bleedColor;
            float rawBleedRate = pawn.health.hediffSet.BleedRateTotal;
            if (EE_Settings.UseMlhBleedRateUnit)
            {
                float mlPerHour = rawBleedRate * maxBloodMl / 24f;
                bleedRateStr = mlPerHour.ToString("F0") + " ml/h";
                bleedColor = rawBleedRate > 1.0f ? colorCrimson : (rawBleedRate > 0.1f ? colorAmber : colorMint);
            }
            else
            {
                float bleedRatePercent = rawBleedRate * 100f;
                bleedRateStr = bleedRatePercent.ToString("F0") + "%/d";
                bleedColor = bleedRatePercent > 100f ? colorCrimson : (bleedRatePercent > 10f ? colorAmber : colorMint);
            }
            DrawGridItem(bleedRect, "BldR", bleedRateStr, bleedColor);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawMonitorScreen(Rect rect, Pawn pawn, CachedVitals vitals)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.02f, 0.02f, 0.025f, 1f));

            float bpm = vitals.displayHeartRate;
            int spo2 = Mathf.RoundToInt(vitals.displaySpO2);

            // Data calculation logic
            float sbp = 120f; float dbp = 80f;
            if (pawn.Dead) { sbp = 0f; dbp = 0f; }
            else
            {
                Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                float bloodLossSeverity = bloodLoss?.Severity ?? 0f;
                float bpVolumeFactor = 1.0f - Mathf.Pow(bloodLossSeverity, 1.5f) * 1.2f;
                bpVolumeFactor = Mathf.Clamp01(bpVolumeFactor);

                float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                float bpPumpingFactor = 1.0f;
                if (pumping <= 0.05f) bpPumpingFactor = 0f;
                else bpPumpingFactor = 0.4f + (pumping * 0.6f);

                sbp = 120f * bpVolumeFactor * bpPumpingFactor;
                dbp = 80f * bpVolumeFactor * bpPumpingFactor;

                Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost);
                if (adrenaline != null)
                {
                    float bonus = adrenaline.Severity * 15f;
                    sbp += bonus; dbp += bonus * 0.7f;
                }
                float noise = Mathf.Sin(Find.TickManager.TicksGame * 0.008f) * 2f;
                sbp += noise; dbp += noise * 0.6f;
                if (sbp < 0f) sbp = 0f; if (dbp < 0f) dbp = 0f;
                if (sbp < dbp) dbp = sbp * 0.67f;
            }

            Color colorCrimson = new Color(0.83f, 0.25f, 0.25f);
            Color colorAmber = new Color(0.85f, 0.60f, 0.25f);
            Color colorMint = new Color(0.32f, 0.78f, 0.52f);
            Color colorCerulean = new Color(0.25f, 0.68f, 0.82f);

            Color hrColor = (bpm < (pawn.Awake() ? 40f : 35f) || bpm > 140f) ? colorCrimson : ((bpm < (pawn.Awake() ? 60f : 50f) || bpm > 100f) ? colorAmber : colorMint);
            Color bpColor = (sbp < 90f || sbp > 140f) ? colorCrimson : colorMint;
            Color spo2Color = (spo2 < 85) ? colorCrimson : ((spo2 < 93) ? colorAmber : colorCerulean);

            Color gridColor;
            Color coreColor;
            Color glowColor;

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

            // Left side: ECG wave (width 300f)
            Rect waveRect = new Rect(innerScreen.x + 2f, innerScreen.y + 10f, 300f, innerScreen.height - 15f);

            // Draw grid in waveRect
            GUI.color = gridColor * new Color(1f, 1f, 1f, 0.5f);
            int horizLines = 5;
            for (int i = 1; i < horizLines; i++)
            {
                float y = waveRect.y + (waveRect.height / horizLines) * i;
                Widgets.DrawLine(new Vector2(waveRect.x, y), new Vector2(waveRect.xMax, y), GUI.color, 1f);
            }
            int vertLines = 8;
            for (int i = 1; i < vertLines; i++)
            {
                float x = waveRect.x + (waveRect.width / vertLines) * i;
                Widgets.DrawLine(new Vector2(x, waveRect.y), new Vector2(x, waveRect.yMax), GUI.color, 1f);
            }
            GUI.color = Color.white;

            float centerY = waveRect.y + waveRect.height / 2f;
            float waveWidthDraw = waveRect.width;

            if (Event.current.type == EventType.Repaint)
            {
                VitalTracker.UpdateWaveform(vitals, bpm);
            }

            int bufferLen = vitals.waveBuffer.Length;
            float scaleX = waveWidthDraw / (bufferLen - 1);

            if (pawn.Dead)
            {
                Widgets.DrawLine(new Vector2(waveRect.x, centerY), new Vector2(waveRect.xMax, centerY), coreColor, 1.2f);
            }
            else
            {
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
                    float screenY1 = centerY - v1 * (waveRect.height * 0.40f);
                    float screenX2 = waveRect.x + (i + 1) * scaleX;
                    float screenY2 = centerY - v2 * (waveRect.height * 0.40f);

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
            }

            // Draw status text
            string statusStr = "EE_Status_Normal".Translate();
            if (pawn.Dead) statusStr = "BIOLOGICAL DEATH";
            else if (bpm < EE_Constants.EcgFlatlineThreshold) statusStr = "EE_Status_CardiacArrest".Translate();
            else if (vitals.hasMyocardialInfarction && bpm > 180f) statusStr = "EE_Status_VFib".Translate();
            else if (bpm > EE_Constants.EcgTachycardiaThreshold) statusStr = "EE_Status_Tachycardia".Translate();
            else if (bpm < (pawn.Awake() ? EE_Constants.EcgBradycardiaThreshold : 35f)) statusStr = "EE_Status_Bradycardia".Translate();

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Vector2 size = Text.CalcSize(statusStr);
            Rect statusRect = new Rect(waveRect.x + 4f, waveRect.yMax - 18f, size.x + 10f, 16f);
            Widgets.DrawBoxSolid(statusRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Widgets.Label(statusRect, statusStr);

            // Draw vertical divider between ECG screen grid and vitals readout (shifted right)
            float lineX = innerScreen.x + 308f;
            Widgets.DrawLine(new Vector2(lineX, innerScreen.y + 4f), new Vector2(lineX, innerScreen.yMax - 4f), new Color(0.3f, 0.3f, 0.3f, 0.3f), 1f);

            // Right side inside the monitor screen: Vitals (HR, BP, SpO2, RR)
            float vitalsX = innerScreen.x + 312f;
            float vitalsWidth = 108f;

            Rect hrRect = new Rect(vitalsX, innerScreen.y + 2f, vitalsWidth, 21f);
            Rect bpRect = new Rect(vitalsX, innerScreen.y + 24f, vitalsWidth, 21f);
            Rect spo2Rect = new Rect(vitalsX, innerScreen.y + 46f, vitalsWidth, 21f);
            Rect rrRect = new Rect(vitalsX, innerScreen.y + 68f, vitalsWidth, 21f);

            string bpStr = (sbp < EE_Constants.BpMeasurableThreshold) ? "--" : string.Format("{0:F0}/{1:F0}", sbp, dbp);
            string spo2StrCompact = (vitals.displaySpO2 < EE_Constants.SpO2MeasurableThreshold) ? "--" : spo2.ToString() + "%";
            float rr = vitals.displayRR;
            string rrStr = rr.ToString("F0");
            Color rrColor = (rr < 8f || rr > 30f) ? colorCrimson : colorMint;

            TextAnchor origAnchor = Text.Anchor;
            DrawVitalVerticalCompact(hrRect, "HR", bpm.ToString("F0"), hrColor, pawn.Dead, GameFont.Medium, true);
            DrawVitalVerticalCompact(bpRect, "BP", bpStr, bpColor, pawn.Dead, GameFont.Small, false);
            DrawVitalVerticalCompact(spo2Rect, "SpO2", spo2StrCompact, spo2Color, pawn.Dead, GameFont.Small, false);
            DrawVitalVerticalCompact(rrRect, "RR", rrStr, rrColor, pawn.Dead, GameFont.Small, false);
            
            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawRightMonitorPanel(Rect rect, Pawn pawn, CachedVitals vitals)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.09f));

            TextAnchor origAnchor = Text.Anchor;
            DrawDiagnosticsInner(rect, pawn);
            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawVitalVerticalCompact(Rect r, string label, string val, Color valColor, bool dead, GameFont font, bool bold)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.5f, 0.7f, 0.8f);
            Widgets.Label(new Rect(r.x, r.y + 2f, 35f, r.height - 4f), label);

            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = valColor;

            string displayVal = dead ? "---" : val;
            if (bold && !dead)
            {
                displayVal = "<b>" + displayVal + "</b>";
            }
            Widgets.Label(new Rect(r.x + 35f, r.y, r.width - 35f, r.height), displayVal);
        }

        private void DrawDiagnosticsInner(Rect rect, Pawn pawn)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.7f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 16f), "/// CLINICAL DIAGNOSIS");

            Rect diagInner = new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 26f);

            List<string> diagnoses = new List<string>();
            List<Color> diagColors = new List<Color>();

            // =========================================================================================
            // CLINICAL TRIAGE COLOR PALETTE (ESI-based Muted Professional Colors)
            // 临床分诊低饱和度专业色表：
            // 1. Level 1 - 红色 (深绯红/Crimson): #D32F2F (0.83f, 0.18f, 0.18f) - 即刻致命危机，需 CPR/除颤/穿刺
            // 2. Level 2 - 橙色 (暖秋橘/Amber): #D97724 (0.85f, 0.47f, 0.14f) - 系统功能衰竭/重度脑缺氧/休克，需监护抢救
            // 3. Level 3 - 黄色 (麦草黄/Straw): #C5A028 (0.77f, 0.63f, 0.16f) - 中度异常/骨折/全身炎症/心动过缓/心动过速
            // 4. Level 4 - 蓝色 (天青蓝/Cerulean): #40A0C0 (0.25f, 0.65f, 0.75f) - 状态受控/已穿刺减压/肾上腺素与吗啡等治疗中
            // 5. Level 5 - 灰色 (板岩灰/Slate): #707070 (0.44f, 0.44f, 0.44f) - 死亡状态 / 未见明显异常
            // =========================================================================================
            Color colCrimson = new Color(0.83f, 0.18f, 0.18f); // Level 1
            Color colAmber = new Color(0.85f, 0.47f, 0.14f);   // Level 2
            Color colStraw = new Color(0.77f, 0.63f, 0.16f);   // Level 3
            Color colCerulean = new Color(0.25f, 0.65f, 0.75f); // Level 4
            Color colSlate = new Color(0.44f, 0.44f, 0.44f);   // Level 5

            if (pawn.Dead)
            {
                diagnoses.Add("EE_Diagnosis_Dead".Translate());
                diagColors.Add(colSlate);
            }
            else
            {
                // 脑死亡倒计时 -> Level 1 (即刻致命)
                Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_BiologicalDeathTimer);
                if (timer != null)
                {
                    float remHours = (1.0f - timer.Severity) * 4.0f;
                    int remGameMinutes = Mathf.RoundToInt(remHours * 60f);
                    diagnoses.Add("EE_Diagnosis_BrainDeathTimer".Translate(remGameMinutes));
                    diagColors.Add(colCrimson);
                }

                // 脑缺氧
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.VegetativeState))
                {
                    // 植物人 -> Level 3 (生命体征稳定，无急性恶化危象)
                    diagnoses.Add("EE_Diagnosis_VegetativeState".Translate());
                    diagColors.Add(colStraw);
                }
                else if (pawn.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia))
                {
                    Hediff ch = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia);
                    if (ch.Severity >= 0.7f)
                    {
                        diagnoses.Add("EE_Diagnosis_CerebralHypoxia_Extreme".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_CerebralHypoxia".Translate());
                        diagColors.Add(colAmber);
                    }
                }

                // 气胸
                Hediff_Pneumothorax pneumo = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Pneumothorax) as Hediff_Pneumothorax;
                if (pneumo != null)
                {
                    if (pneumo.isDecompressed)
                    {
                        // 气胸已减压 -> Level 4 (稳定受控)
                        diagnoses.Add("EE_Diagnosis_PneumothoraxDecompressed".Translate());
                        diagColors.Add(colCerulean);
                    }
                    else
                    {
                        // 张力性气胸危象 -> Level 1 (即刻致命)
                        diagnoses.Add("EE_Diagnosis_PneumothoraxCrisis".Translate());
                        diagColors.Add(colCrimson);
                    }
                }

                // 心血管危象
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction))
                {
                    Hediff mi = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
                    if (mi.Severity >= 0.60f)
                    {
                        // 心脏骤停 -> Level 1 (即刻致命)
                        diagnoses.Add("EE_Diagnosis_CardiacArrest".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        // 心室颤动 -> Level 1 (即刻致命)
                        diagnoses.Add("EE_Diagnosis_VFib".Translate());
                        diagColors.Add(colCrimson);
                    }
                }

                // 休克与大出血
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Shock))
                {
                    Hediff shock = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Shock);
                    if (shock.Severity >= 0.7f)
                    {
                        diagnoses.Add("EE_Diagnosis_Shock_Irreversible".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_Shock".Translate());
                        diagColors.Add(colAmber);
                    }
                }
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.MassiveBleeding))
                {
                    // 活动性大出血 -> Level 1 (即刻致命)
                    diagnoses.Add("EE_Diagnosis_MassiveBleeding".Translate());
                    diagColors.Add(colCrimson);
                }

                // 代谢性酸中毒
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis))
                {
                    Hediff acid = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
                    if (acid.Severity >= 0.5f)
                    {
                        diagnoses.Add("EE_Diagnosis_Acidosis_Extreme".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_Acidosis".Translate());
                        diagColors.Add(colAmber);
                    }
                }

                // 败血症与SIRS
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Sepsis))
                {
                    Hediff sepsis = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Sepsis);
                    if (sepsis.Severity >= 0.8f)
                    {
                        diagnoses.Add("EE_Diagnosis_Sepsis_Extreme".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_Sepsis".Translate());
                        diagColors.Add(colAmber);
                    }
                }
                else if (pawn.health.hediffSet.HasHediff(EE_DefOf.SIRS))
                {
                    Hediff sirs = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.SIRS);
                    if (sirs.Severity >= 0.7f)
                    {
                        diagnoses.Add("EE_Diagnosis_SIRS_Late".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_SIRS".Translate());
                        diagColors.Add(colStraw);
                    }
                }

                // 器官衰竭
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.MultipleOrganFailure))
                {
                    Hediff mods = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MultipleOrganFailure);
                    if (mods.Severity >= 0.8f)
                    {
                        diagnoses.Add("EE_Diagnosis_MODS_Terminal".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_MODS".Translate());
                        diagColors.Add(colAmber);
                    }
                }

                // 凝血功能障碍
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.Coagulopathy))
                {
                    Hediff coag = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.Coagulopathy);
                    if (coag.Severity >= 0.7f)
                    {
                        diagnoses.Add("EE_Diagnosis_Coagulopathy_Severe".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else
                    {
                        diagnoses.Add("EE_Diagnosis_Coagulopathy".Translate());
                        diagColors.Add(colAmber);
                    }
                }

                // 骨折检查
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff is Hediff_Fracture fracture)
                    {
                        bool isFixed = fracture.isSplinted || fracture.isCasted || fracture.isInternallyFixed || fracture.isStrictBedrest;
                        if (!isFixed)
                        {
                            // 未固定骨折 -> Level 3 (中度警示)
                            diagnoses.Add("EE_Diagnosis_UnfixedFracture".Translate(fracture.Part.Label));
                            diagColors.Add(colStraw);
                        }
                    }
                }

                // 动态心律失常检测 -> Level 3 (中度警示)
                CachedVitals vitals = VitalTracker.GetOrCreateVitals(pawn);
                if (vitals != null)
                {
                    float bpm = vitals.displayHeartRate;
                    if (bpm >= EE_Constants.EcgFlatlineThreshold)
                    {
                        if (bpm > 140f)
                        {
                            diagnoses.Add("EE_Diagnosis_Tachycardia".Translate());
                            diagColors.Add(colStraw);
                        }
                        else if (bpm < (pawn.Awake() ? 45f : 35f))
                        {
                            diagnoses.Add("EE_Diagnosis_Bradycardia".Translate());
                            diagColors.Add(colStraw);
                        }
                    }
                }

                // 肾上腺素强化生效状态检测 (包含过量分级)
                Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost);
                if (adrenaline != null)
                {
                    float sev = adrenaline.Severity;
                    if (sev >= 2.0f) // 致命过量 (儿茶酚胺风暴) -> Level 1 (红色危机)
                    {
                        diagnoses.Add("EE_Diagnosis_AdrenalineFatalOverdose".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else if (sev >= 1.5f) // 药物过量 (交感过负荷) -> Level 2 (橙色危象)
                    {
                        diagnoses.Add("EE_Diagnosis_AdrenalineOverdose".Translate());
                        diagColors.Add(colAmber);
                    }
                    else // 治疗正常生效中 -> Level 4 (天青绿)
                    {
                        diagnoses.Add("EE_Diagnosis_AdrenalineActive".Translate());
                        diagColors.Add(colCerulean);
                    }
                }

                // 吗啡治疗生效状态检测 (包含过量分级)
                Hediff morphine = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MorphineActive);
                if (morphine != null)
                {
                    float sev = morphine.Severity;
                    if (sev >= 2.0f) // 吗啡中毒 (深昏迷) -> Level 1 (红色危机)
                    {
                        diagnoses.Add("EE_Diagnosis_MorphineFatalOverdose".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else if (sev >= 1.0f) // 吗啡过量 (中枢抑制) -> Level 2 (橙色危象)
                    {
                        diagnoses.Add("EE_Diagnosis_MorphineOverdose".Translate());
                        diagColors.Add(colAmber);
                    }
                    else // 治疗正常生效中 -> Level 4 (天青绿)
                    {
                        diagnoses.Add("EE_Diagnosis_MorphineActive".Translate());
                        diagColors.Add(colCerulean);
                    }
                }

                // 氨甲环酸生效状态检测 (包含过量分级)
                Hediff txa = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_TxaActive);
                if (txa != null)
                {
                    float sev = txa.Severity;
                    if (sev >= 2.0f) // 氨甲环酸中毒 (致命血栓/抽搐) -> Level 1 (红色危机)
                    {
                        diagnoses.Add("EE_Diagnosis_TxaFatalOverdose".Translate());
                        diagColors.Add(colCrimson);
                    }
                    else if (sev >= 1.0f) // 氨甲环酸过量 (高凝积蓄) -> Level 2 (橙色危象)
                    {
                        diagnoses.Add("EE_Diagnosis_TxaOverdose".Translate());
                        diagColors.Add(colAmber);
                    }
                    else // 治疗正常生效中 -> Level 4 (天青绿)
                    {
                        diagnoses.Add("EE_Diagnosis_TxaActive".Translate());
                        diagColors.Add(colCerulean);
                    }
                }
            }

            // 按照严重度优先级对诊断进行排序：Crimson(1) > Amber(2) > Straw(3) > Cerulean(4) > Slate(5)
            List<string> sortedDiagnoses = new List<string>();
            List<Color> sortedColors = new List<Color>();
            for (int p = 1; p <= 5; p++)
            {
                for (int i = 0; i < diagnoses.Count; i++)
                {
                    Color col = diagColors[i];
                    int priority = 5;
                    if (col == colCrimson) priority = 1;
                    else if (col == colAmber) priority = 2;
                    else if (col == colStraw) priority = 3;
                    else if (col == colCerulean) priority = 4;

                    if (priority == p)
                    {
                        sortedDiagnoses.Add(diagnoses[i]);
                        sortedColors.Add(diagColors[i]);
                    }
                }
            }
            diagnoses = sortedDiagnoses;
            diagColors = sortedColors;

            if (diagnoses.Count == 0)
            {
                diagnoses.Add("EE_Diagnosis_None".Translate());
                diagColors.Add(colSlate);
            }

            float curX = diagInner.x;
            float curY = diagInner.y;
            Text.Font = GameFont.Small;
            for (int i = 0; i < diagnoses.Count; i++)
            {
                Vector2 size = Text.CalcSize(diagnoses[i]);
                float tagWidth = size.x + 18f;
                float tagHeight = 22f;

                if (curX + tagWidth > diagInner.xMax)
                {
                    curX = diagInner.x;
                    curY += 26f;
                    if (curY + tagHeight > diagInner.yMax) break;
                }

                Rect tagRect = new Rect(curX, curY, tagWidth, tagHeight);
                Widgets.DrawBoxSolid(tagRect, new Color(0.12f, 0.12f, 0.13f));
                Widgets.DrawBoxSolid(new Rect(tagRect.x, tagRect.y, 3f, tagRect.height), diagColors[i]);
                
                GUI.color = diagColors[i];
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(tagRect.x + 9f, tagRect.y + 1f, tagRect.width - 10f, tagRect.height - 2f), diagnoses[i]);
                
                curX += tagWidth + 6f;
            }
            GUI.color = Color.white;
        }





        private void DrawQuickActions(Rect rect, Pawn patient)
        {
            float spacing = 6f;
            float btnW = (rect.width - (spacing * 3f)) / 4f; 
            float btnH = rect.height;

            Rect cprBtn = new Rect(rect.x, rect.y, btnW, btnH);
            Rect decompBtn = new Rect(rect.x + btnW + spacing, rect.y, btnW, btnH);
            Rect aidBtn = new Rect(rect.x + (btnW + spacing) * 2f, rect.y, btnW, btnH);
            Rect defibBtn = new Rect(rect.x + (btnW + spacing) * 3f, rect.y, btnW, btnH);

            bool DrawFlatButton(Rect btnRect, string text, Color accentColor, bool active)
            {
                if (!active)
                {
                    // 未激活状态使用极低透明度背景和灰色文字
                    Widgets.DrawBoxSolid(btnRect, new Color(0.08f, 0.08f, 0.09f, 0.6f));
                    Text.Font = GameFont.Small;
                    GUI.color = new Color(0.35f, 0.35f, 0.35f);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(btnRect, text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    return false;
                }

                bool clicked = Widgets.ButtonInvisible(btnRect);
                
                // 激活状态背景色 (高对比度深蓝灰)，悬停高亮
                Color bgCol = Mouse.IsOver(btnRect) ? new Color(0.25f, 0.27f, 0.32f) : new Color(0.18f, 0.19f, 0.22f);
                Widgets.DrawBoxSolid(btnRect, bgCol);
                
                // 底部 2px 装饰彩条
                Widgets.DrawBoxSolid(new Rect(btnRect.x, btnRect.yMax - 2f, btnRect.width, 2f), accentColor);
                
                // 30% 透明度的彩色细边框，提升科技感
                GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f);
                Widgets.DrawBox(btnRect, 1);
                GUI.color = Color.white;
                
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(btnRect, text);
                Text.Anchor = TextAnchor.UpperLeft;
                
                return clicked;
            }

            // 1. CPR
            bool needsCpr = false;
            if (patient.Downed && !patient.Dead)
            {
                if (EE_DefOf.EE_MyocardialInfarction != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction)) needsCpr = true;
                else if (EE_DefOf.EE_BiologicalDeathTimer != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer)) needsCpr = true;
                else
                {
                    float pumping = patient.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                    float breathing = patient.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                    if (pumping <= EE_Constants.VitalFlatlineThreshold && breathing <= EE_Constants.VitalFlatlineThreshold) needsCpr = true;
                }
            }

            Color btnAccentColor = new Color(0.25f, 0.65f, 0.75f);

            string cprTxt = "CPR";
            if (DrawFlatButton(cprBtn, cprTxt, btnAccentColor, needsCpr))
            {
                OrderCPR(patient);
            }

            // 2. Decompress
            bool needsDecomp = false;
            if (!patient.Dead && EE_DefOf.EE_Pneumothorax != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_Pneumothorax))
            {
                var pneumo = patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Pneumothorax) as Hediff_Pneumothorax;
                if (pneumo != null && !pneumo.isDecompressed) needsDecomp = true;
            }

            string decompTxt = (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName.Contains("Chinese")) ? "穿刺减压" : "Decompress";
            if (DrawFlatButton(decompBtn, decompTxt, btnAccentColor, needsDecomp))
            {
                OrderNeedleDecompression(patient);
            }

            // 3. Aid
            string aidTxt = (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName.Contains("Chinese")) ? "背包急救" : "First Aid";
            if (DrawFlatButton(aidBtn, aidTxt, btnAccentColor, !patient.Dead))
            {
                OrderFastFirstAid(patient);
            }

            // 4. Defib (Replaced Declare Death)
            bool needsDefib = false;
            if (!patient.Dead && EE_DefOf.EE_MyocardialInfarction != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction))
            {
                needsDefib = true; // For now just enable if MI
            }
            string defibTxt = (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName.Contains("Chinese")) ? "除颤" : "Defib";
            if (DrawFlatButton(defibBtn, defibTxt, btnAccentColor, needsDefib))
            {
                OrderDefib(patient);
            }
        }

        private void OrderCPR(Pawn patient)
        {
            if (patient == null || patient.Map == null) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (Pawn doctor in patient.Map.mapPawns.FreeColonistsSpawned)
            {
                if (doctor.Downed || doctor.Dead || !doctor.Faction.IsPlayer) continue;
                if (!doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;

                string label = doctor.LabelShort;
                if (doctor.workSettings != null && doctor.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                {
                    label += " (" + "EE_Doctor".Translate() + ")";
                }

                options.Add(new FloatMenuOption(label, () =>
                {
                    Job job = JobMaker.MakeJob(EE_DefOf.EE_PerformCPR, patient);
                    doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("EE_NoDoctorsAvailable".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        private void OrderDefib(Pawn patient)
        {
            if (patient == null || patient.Map == null) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (Pawn doctor in patient.Map.mapPawns.FreeColonistsSpawned)
            {
                if (doctor.Downed || doctor.Dead || !doctor.Faction.IsPlayer) continue;
                if (!doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;

                List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(doctor);
                Thing defib = availableItems.FirstOrDefault(t => EE_FirstAidUtility.GetEmergencyItemType(t.def) == EmergencyItemType.Defibrillator);
                if (defib != null)
                {
                    string label = doctor.LabelShort;
                    if (doctor.workSettings != null && doctor.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                    {
                        label += " (" + "EE_Doctor".Translate() + ")";
                    }
                    string optionLabel = $"{doctor.LabelShort} - {"EE_OrderPerformDefibrillation".Translate(patient.LabelShort, defib.stackCount)}";

                    options.Add(new FloatMenuOption(optionLabel, () =>
                    {
                        if (doctor.Drafted) doctor.drafter.Drafted = false;

                        Job job = JobMaker.MakeJob(EE_DefOf.EE_ApplyFirstAid, patient, defib);
                        job.count = 1;
                        doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("EE_NoDefibrillatorAvailable".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        private void OrderNeedleDecompression(Pawn patient)
        {
            if (patient == null) return;
            RecipeDef recipe = EE_DefOf.EE_Recipe_NeedleDecompression;
            if (recipe == null) return;

            if (!patient.BillStack.Bills.Any(b => b.recipe == recipe))
            {
                Bill_Medical bill = new Bill_Medical(recipe, null);
                patient.BillStack.AddBill(bill);
            }

            if (patient.CurrentBed() == null)
            {
                Messages.Message("EE_NeedleDecompressionBillAdded_NoBed".Translate(patient.LabelShort), MessageTypeDefOf.CautionInput, false);
            }
            else
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (Pawn doctor in patient.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (doctor.Downed || doctor.Dead || !doctor.Faction.IsPlayer) continue;
                    if (!doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                        !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;

                    Bill bill = patient.BillStack.Bills.FirstOrDefault(b => b.recipe == recipe);
                    if (bill != null)
                    {
                        options.Add(new FloatMenuOption(doctor.LabelShort, () =>
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.DoBill, patient.CurrentBed());
                            job.bill = bill;
                            doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }));
                    }
                }

                if (options.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                else
                {
                    Messages.Message("EE_NoDoctorsAvailable".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        private void OrderFastFirstAid(Pawn patient)
        {
            if (patient == null || patient.Map == null) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (Pawn doctor in patient.Map.mapPawns.FreeColonistsSpawned)
            {
                if (doctor.Downed || doctor.Dead || !doctor.Faction.IsPlayer) continue;
                if (!doctor.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                    !doctor.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;

                List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(doctor);
                if (availableItems.Count == 0) continue;

                var grouped = availableItems.GroupBy(t => t.def);
                foreach (var group in grouped)
                {
                    ThingDef itemDef = group.Key;
                    Thing firstThing = group.First();
                    int totalCount = group.Sum(t => t.stackCount);
                    EmergencyItemType type = EE_FirstAidUtility.GetEmergencyItemType(itemDef);

                    if (EE_FirstAidUtility.CanApplyToTarget(patient, type, itemDef))
                    {
                        string optionLabel = $"{doctor.LabelShort} - {"EE_OrderUseItemOn".Translate(patient.LabelShort, itemDef.LabelCap, totalCount)}";

                        options.Add(new FloatMenuOption(optionLabel, () =>
                        {
                            if (doctor.Drafted) doctor.drafter.Drafted = false;

                            Job job = JobMaker.MakeJob(EE_DefOf.EE_ApplyFirstAid, patient, firstThing);
                            job.count = 1;
                            doctor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        }));
                    }
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("EE_NoDoctorsAvailable".Translate() + " (或者没有携带合适的急救品)", MessageTypeDefOf.RejectInput, false);
            }
        }

        private void OrderDeclareDeath(Pawn patient)
        {
            Find.WindowStack.Add(new Dialog_MessageBox(
                "EE_ConfirmDeclareDeathDesc".Translate(patient.NameShortColored),
                "EE_Confirm".Translate(),
                delegate ()
                {
                    if (patient == null || patient.Dead) return;

                    Hediff deathCause = HediffMaker.MakeHediff(EE_DefOf.EE_DeclaredDead, patient, null);
                    patient.health.AddHediff(deathCause, null, null, null);
                    patient.Kill(null, deathCause);
                },
                "EE_Cancel".Translate()
            ));
        }
    }
}
