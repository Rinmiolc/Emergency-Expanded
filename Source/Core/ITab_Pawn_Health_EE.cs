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
                float width = this.IsNiceHealthTabActive ? 780f : 630f;
                return new Vector2(width, 700f);
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
                float width = this.IsNiceHealthTabActive ? 780f : 630f;
                return new Vector2(width, 430f);
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

            // 1. 顶部屏幕：ECG波形 + 核心三大体征 (HR, BP, SpO2)
            Rect monitorRect = new Rect(inner.x, inner.y, inner.width, 95f);
            DrawMonitorScreen(monitorRect, pawn, vitals);

            // 2. 底部功能区：分为左右两块
            // 左侧：2x2体征网格 + 4个急救按钮
            // 右侧：临床诊断标签
            Rect leftPanel = new Rect(inner.x, inner.y + 100f, 360f, 90f);
            Rect rightPanel = new Rect(inner.x + 370f, inner.y + 100f, inner.width - 370f, 90f);

            Rect gridRect = new Rect(leftPanel.x, leftPanel.y, leftPanel.width, 48f);
            DrawSecondaryGrid(gridRect, pawn, vitals);

            Rect actionsRect = new Rect(leftPanel.x, leftPanel.y + 55f, leftPanel.width, 35f);
            DrawQuickActions(actionsRect, pawn);

            DrawDiagnosticsBox(rightPanel, pawn);
        }

        private void DrawSecondaryGrid(Rect rect, Pawn pawn, CachedVitals vitals)
        {
            float colWidth = rect.width / 2f - 4f;
            float rowHeight = rect.height / 2f;

            Rect rrRect = new Rect(rect.x, rect.y, colWidth, rowHeight);
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

            float ph = 7.40f;
            Hediff acidosis = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
            if (acidosis != null) ph -= acidosis.Severity * 0.55f;
            ph += (Mathf.PingPong(Time.realtimeSinceStartup * 0.05f, 0.02f) - 0.01f);
            ph = Mathf.Clamp(ph, 6.70f, 7.45f);

            float rr = 16f;
            if (pawn.Dead) { rr = 0f; }
            else
            {
                float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                if (breathing <= 0.05f) rr = 0f;
                else
                {
                    rr = 16f * breathing;
                    if (acidosis != null) rr += acidosis.Severity * 14f;
                    Hediff morphine = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MorphineActive);
                    if (morphine != null) rr -= morphine.Severity * 6f;
                    if (pawn.health.hediffSet.BleedRateTotal > 0.1f) rr += Mathf.Clamp(pawn.health.hediffSet.BleedRateTotal * 8f, 0f, 12f);
                    rr += Mathf.Sin(Time.realtimeSinceStartup * 0.3f) * 1f;
                    rr = Mathf.Clamp(rr, 0f, 45f);
                }
            }
            
            float bleedRate = pawn.health.hediffSet.BleedRateTotal * 100f;

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

            Color rrColor = (rr < 8f || rr > 30f) ? Color.red : new Color(0.2f, 1.0f, 0.5f);
            DrawGridItem(rrRect, "RR", rr.ToString("F0"), rrColor);

            Color phColor = (ph < 7.30f) ? Color.red : new Color(0.2f, 1.0f, 0.5f);
            DrawGridItem(phRect, "pH", ph.ToString("F2"), phColor);

            Color volColor = bloodLoss?.Severity > 0.4f ? Color.red : (bloodLoss?.Severity > 0.15f ? Color.yellow : new Color(0.2f, 1.0f, 0.5f));
            DrawGridItem(volRect, "Vol", volPct.ToString("F0") + "% (" + currentBloodMl.ToString("F0") + "ml)", volColor);

            Color bleedColor = bleedRate > 100f ? Color.red : (bleedRate > 10f ? Color.yellow : new Color(0.2f, 1.0f, 0.5f));
            DrawGridItem(bleedRect, "BldR", bleedRate.ToString("F0") + "%/d", bleedColor);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawMonitorScreen(Rect rect, Pawn pawn, CachedVitals vitals)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.02f, 0.02f, 0.025f, 1f));

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

            // 绘制精细网格 (简化为非常柔和的虚线感)
            GUI.color = gridColor * new Color(1f, 1f, 1f, 0.5f);
            int horizLines = 5;
            for (int i = 1; i < horizLines; i++)
            {
                float y = innerScreen.y + (innerScreen.height / horizLines) * i;
                Widgets.DrawLine(new Vector2(innerScreen.x, y), new Vector2(innerScreen.xMax, y), GUI.color, 1f);
            }
            int vertLines = 12;
            for (int i = 1; i < vertLines; i++)
            {
                float x = innerScreen.x + (innerScreen.width / vertLines) * i;
                Widgets.DrawLine(new Vector2(x, innerScreen.y), new Vector2(x, innerScreen.yMax), GUI.color, 1f);
            }
            GUI.color = Color.white;

            // Dynamically scale wave width and right panel width to prevent stretching the ECG curves
            float waveWidth = 460f;
            float rightPanelWidth = innerScreen.width - waveWidth - 10f;
            bool isWidescreen = innerScreen.width >= 650f;

            if (!isWidescreen)
            {
                rightPanelWidth = 140f;
                waveWidth = innerScreen.width - rightPanelWidth - 10f;
            }

            Rect waveRect = new Rect(innerScreen.x + 2f, innerScreen.y + 10f, waveWidth, innerScreen.height - 15f);
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

            string statusStr = "EE_Status_Normal".Translate();
            if (pawn.Dead) statusStr = "BIOLOGICAL DEATH";
            else if (bpm < EE_Constants.EcgFlatlineThreshold) statusStr = "EE_Status_CardiacArrest".Translate();
            else if (vitals.hasMyocardialInfarction && bpm > 180f) statusStr = "EE_Status_VFib".Translate();
            else if (bpm > EE_Constants.EcgTachycardiaThreshold) statusStr = "EE_Status_Tachycardia".Translate();
            else if (bpm < EE_Constants.EcgBradycardiaThreshold) statusStr = "EE_Status_Bradycardia".Translate();

            // 波形图右上角/正下方绘制诊断文字
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Vector2 size = Text.CalcSize(statusStr);
            Rect statusRect = new Rect(waveRect.x + 4f, waveRect.yMax - 18f, size.x + 10f, 16f);
            Widgets.DrawBoxSolid(statusRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Widgets.Label(statusRect, statusStr);

            // ================= 右侧核心体征区 =================
            // 分割线
            Widgets.DrawLine(new Vector2(waveRect.xMax + 5f, innerScreen.y + 5f), new Vector2(waveRect.xMax + 5f, innerScreen.yMax - 5f), new Color(0.2f, 0.2f, 0.2f, 0.5f), 1f);

            Rect rightPanel = new Rect(innerScreen.xMax - rightPanelWidth + 10f, innerScreen.y + 2f, rightPanelWidth - 10f, innerScreen.height - 4f);
            TextAnchor origAnchor = Text.Anchor;

            // 数据计算逻辑
            float sbp = 120f; float dbp = 80f;
            if (pawn.Dead) { sbp = 0f; dbp = 0f; }
            else
            {
                float bloodVolume = 1f;
                Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodLoss != null) bloodVolume = Mathf.Clamp01(1f - bloodLoss.Severity);
                float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);

                // Quadratic blood volume factor representing vascular compensation (non-linear drop)
                float bpVolumeFactor = 1.0f - Mathf.Pow(1.0f - bloodVolume, 2f) * 2f;
                bpVolumeFactor = Mathf.Clamp01(bpVolumeFactor);

                // Pumping capacity compensation: vasoconstriction (SVR) helps maintain BP even if contractility falls
                float bpPumpingFactor = 1.0f;
                if (pumping <= 0.05f)
                {
                    bpPumpingFactor = 0f;
                }
                else
                {
                    bpPumpingFactor = 0.4f + (pumping * 0.6f);
                }

                sbp = 120f * bpVolumeFactor * bpPumpingFactor;
                dbp = 80f * bpVolumeFactor * bpPumpingFactor;

                Hediff adrenaline = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost);
                if (adrenaline != null)
                {
                    float bonus = adrenaline.Severity * 15f;
                    sbp += bonus; dbp += bonus * 0.7f;
                }
                float noise = Mathf.Sin(Time.realtimeSinceStartup * 0.5f) * 2f;
                sbp += noise; dbp += noise * 0.6f;
                if (sbp < 0f) sbp = 0f; if (dbp < 0f) dbp = 0f;
                if (sbp < dbp) dbp = sbp * 0.67f;
            }

            float maxBloodMl = 5000f * pawn.BodySize;
            float bloodLossSev = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
            float currentBloodMl = maxBloodMl * Mathf.Clamp01(1f - bloodLossSev);
            float volPct = currentBloodMl / maxBloodMl * 100f;

            float ph = 7.40f;
            Hediff acidosis = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis);
            if (acidosis != null) ph -= acidosis.Severity * 0.55f;
            ph += (Mathf.PingPong(Time.realtimeSinceStartup * 0.05f, 0.02f) - 0.01f);
            ph = Mathf.Clamp(ph, 6.70f, 7.45f);

            float rr = 16f;
            if (pawn.Dead) { rr = 0f; }
            else
            {
                float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                if (breathing <= 0.05f) rr = 0f;
                else
                {
                    rr = 16f * breathing;
                    if (acidosis != null) rr += acidosis.Severity * 14f;
                    Hediff morphine = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MorphineActive);
                    if (morphine != null) rr -= morphine.Severity * 6f;
                    if (pawn.health.hediffSet.BleedRateTotal > 0.1f) rr += Mathf.Clamp(pawn.health.hediffSet.BleedRateTotal * 8f, 0f, 12f);
                    rr += Mathf.Sin(Time.realtimeSinceStartup * 0.3f) * 1f;
                    rr = Mathf.Clamp(rr, 0f, 45f);
                }
            }

            Color hrColor = (bpm < 40f || bpm > 140f) ? Color.red : ((bpm < 60f || bpm > 100f) ? Color.yellow : new Color(0.2f, 1.0f, 0.5f));
            Color bpColor = (sbp < 90f || sbp > 140f) ? Color.red : new Color(0.2f, 1.0f, 0.5f);
            Color spo2Color = (spo2 < 85) ? Color.red : ((spo2 < 93) ? Color.yellow : new Color(0.0f, 0.9f, 1.0f));

            if (isWidescreen)
            {
                // Horizontal 3-column layout
                float colWidth = rightPanel.width / 3f;
                Rect hrRect = new Rect(rightPanel.x, rightPanel.y, colWidth - 4f, rightPanel.height);
                Rect bpRect = new Rect(rightPanel.x + colWidth, rightPanel.y, colWidth - 4f, rightPanel.height);
                Rect spo2Rect = new Rect(rightPanel.x + colWidth * 2f, rightPanel.y, colWidth - 4f, rightPanel.height);

                DrawVitalHorizontal(hrRect, "HR", bpm.ToString("F0"), hrColor, pawn.Dead);
                DrawVitalHorizontal(bpRect, "BP", string.Format("{0:F0}/{1:F0}", sbp, dbp), bpColor, pawn.Dead);
                DrawVitalHorizontal(spo2Rect, "SpO2", spo2.ToString() + "%", spo2Color, pawn.Dead);
            }
            else
            {
                // Vertical 3-row layout
                float rowHeight = rightPanel.height / 3f;
                Rect hrRect = new Rect(rightPanel.x, rightPanel.y, rightPanel.width, rowHeight);
                Rect bpRect = new Rect(rightPanel.x, rightPanel.y + rowHeight, rightPanel.width, rowHeight);
                Rect spo2Rect = new Rect(rightPanel.x, rightPanel.y + rowHeight * 2f, rightPanel.width, rowHeight);

                DrawVital1x3(hrRect, "HR", bpm.ToString("F0"), hrColor, pawn.Dead);
                DrawVital1x3(bpRect, "BP", string.Format("{0:F0}/{1:F0}", sbp, dbp), bpColor, pawn.Dead);
                DrawVital1x3(spo2Rect, "SpO2", spo2.ToString() + "%", spo2Color, pawn.Dead);
            }

            Text.Anchor = origAnchor;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawVital1x3(Rect r, string label, string val, Color valColor, bool dead)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.5f, 0.7f, 0.8f); // uniform cyan-gray label
            Widgets.Label(new Rect(r.x, r.y + 2f, 40f, r.height), label);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = valColor;
            Widgets.Label(new Rect(r.x + 35f, r.y, r.width - 40f, r.height), dead ? "---" : val);
        }

        private void DrawVitalHorizontal(Rect r, string label, string val, Color valColor, bool dead)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = new Color(0.5f, 0.7f, 0.8f); // uniform cyan-gray label
            Widgets.Label(new Rect(r.x, r.y + 2f, r.width, 16f), label);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = valColor;
            Widgets.Label(new Rect(r.x, r.y + 18f, r.width, r.height - 20f), dead ? "---" : val);
        }

        private void DrawDiagnosticsBox(Rect rect, Pawn pawn)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.09f));

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.7f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 16f), "/// CLINICAL DIAGNOSIS");

            Rect diagInner = new Rect(rect.x + 4f, rect.y + 22f, rect.width - 8f, rect.height - 26f);

            List<string> diagnoses = new List<string>();
            List<Color> diagColors = new List<Color>();

            if (pawn.Dead)
            {
                diagnoses.Add("生物学死亡");
                diagColors.Add(Color.gray);
            }
            else
            {
                // 脑死亡倒计时
                Hediff timer = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_BiologicalDeathTimer);
                if (timer != null)
                {
                    float remHours = (1.0f - timer.Severity) * 4.0f;
                    int remGameMinutes = Mathf.RoundToInt(remHours * 60f);
                    diagnoses.Add($"脑死亡倒计时: {remGameMinutes}m");
                    diagColors.Add(Color.red);
                }

                // 脑缺氧
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.VegetativeState))
                {
                    diagnoses.Add("脑坏死 (植物人)");
                    diagColors.Add(Color.red);
                }
                else if (pawn.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia))
                {
                    diagnoses.Add("脑缺氧 (进行性神经损伤)");
                    diagColors.Add(Color.red);
                }

                // 气胸
                Hediff_Pneumothorax pneumo = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Pneumothorax) as Hediff_Pneumothorax;
                if (pneumo != null)
                {
                    if (pneumo.isDecompressed)
                    {
                        diagnoses.Add("气胸 (已穿刺减压)");
                        diagColors.Add(Color.green);
                    }
                    else
                    {
                        diagnoses.Add("张力性气胸 (窒息危象)");
                        diagColors.Add(Color.red);
                    }
                }

                // 心血管危象
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction))
                {
                    Hediff mi = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
                    if (mi.Severity >= 0.60f)
                    {
                        diagnoses.Add("心脏骤停 (Flatline)");
                        diagColors.Add(Color.red);
                    }
                    else
                    {
                        diagnoses.Add("心室颤动 (VFib)");
                        diagColors.Add(Color.red);
                    }
                }

                // 休克与大出血
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Shock))
                {
                    diagnoses.Add("休克危象");
                    diagColors.Add(Color.red);
                }
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.MassiveBleeding))
                {
                    diagnoses.Add("活动性大出血!");
                    diagColors.Add(Color.red);
                }

                // 败血症与SIRS
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Sepsis))
                {
                    diagnoses.Add("严重败血症");
                    diagColors.Add(Color.red);
                }
                else if (pawn.health.hediffSet.HasHediff(EE_DefOf.SIRS))
                {
                    diagnoses.Add("全身炎症 (SIRS)");
                    diagColors.Add(Color.yellow);
                }

                // 器官衰竭
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.MultipleOrganFailure))
                {
                    diagnoses.Add("多器官衰竭 (MODS)");
                    diagColors.Add(Color.red);
                }

                // 骨折检查
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff is Hediff_Fracture fracture)
                    {
                        bool isFixed = fracture.isSplinted || fracture.isCasted || fracture.isInternallyFixed || fracture.isStrictBedrest;
                        if (!isFixed)
                        {
                            diagnoses.Add($"{fracture.Part.Label}未固定骨折");
                            diagColors.Add(Color.red);
                        }
                    }
                }
            }

            if (diagnoses.Count == 0)
            {
                diagnoses.Add("EE_Diagnosis_None".Translate());
                diagColors.Add(Color.gray);
            }

            float curX = diagInner.x;
            float curY = diagInner.y;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < diagnoses.Count; i++)
            {
                Vector2 size = Text.CalcSize(diagnoses[i]);
                float tagWidth = size.x + 16f;

                if (curX + tagWidth > diagInner.xMax)
                {
                    curX = diagInner.x;
                    curY += 18f;
                    if (curY + 16f > diagInner.yMax) break;
                }

                Rect tagRect = new Rect(curX, curY, tagWidth, 16f);
                Widgets.DrawBoxSolid(tagRect, new Color(0.12f, 0.12f, 0.13f));
                Widgets.DrawBoxSolid(new Rect(tagRect.x, tagRect.y, 3f, tagRect.height), diagColors[i]);
                
                GUI.color = diagColors[i];
                Widgets.Label(new Rect(tagRect.x + 8f, tagRect.y + 1f, tagRect.width - 10f, 16f), diagnoses[i]);
                
                curX += tagWidth + 6f;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
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
                    Widgets.DrawBoxSolid(btnRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                    Text.Font = GameFont.Small;
                    GUI.color = new Color(0.4f, 0.4f, 0.4f);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(btnRect, text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    return false;
                }

                bool clicked = Widgets.ButtonInvisible(btnRect);
                if (Mouse.IsOver(btnRect)) {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.18f, 0.18f, 0.19f));
                } else {
                    Widgets.DrawBoxSolid(btnRect, new Color(0.12f, 0.12f, 0.13f));
                }
                Widgets.DrawBoxSolid(new Rect(btnRect.x, btnRect.y, btnRect.width, 2f), accentColor);
                
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

            string cprTxt = "CPR";
            if (DrawFlatButton(cprBtn, cprTxt, new Color(1.0f, 0.3f, 0.3f), needsCpr))
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
            if (DrawFlatButton(decompBtn, decompTxt, new Color(1.0f, 0.6f, 0.2f), needsDecomp))
            {
                OrderNeedleDecompression(patient);
            }

            // 3. Aid
            string aidTxt = (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName.Contains("Chinese")) ? "背包急救" : "First Aid";
            if (DrawFlatButton(aidBtn, aidTxt, new Color(0.2f, 0.8f, 0.4f), !patient.Dead))
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
            if (DrawFlatButton(defibBtn, defibTxt, new Color(0.2f, 0.6f, 1.0f), needsDefib))
            {
                // Just use CPR order logic for Defib temporarily as requested
                OrderCPR(patient);
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
