using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EmergencyExpanded
{
    [StaticConstructorOnStartup]
    public static class Patch_HealthCardUtility_UI
    {
        public static bool isDrawingHealthTab = false;
        public static int lastHealthTabDrawFrame = -1;
        public static bool useDarkMode = true; // Dark mode toggle

        public static readonly Texture2D BleedIcon = AccessTools.Field(typeof(HealthCardUtility), "BleedingIcon").GetValue(null) as Texture2D;
    }

    [HarmonyPatch(typeof(Widgets), "DrawMenuSection", new Type[] { typeof(Rect) })]
    public static class Patch_Widgets_DrawMenuSection
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect)
        {
            if (Patch_HealthCardUtility_UI.isDrawingHealthTab && Patch_HealthCardUtility_UI.useDarkMode)
            {
                // Draw modern dark background instead of vanilla MenuSection texture
                Widgets.DrawBoxSolid(rect, new Color(0.14f, 0.14f, 0.15f, 1f));
                GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
                Widgets.DrawBox(rect, 1); // Subtle border
                GUI.color = Color.white;
                return false; // Skip original drawing
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffListing")]
    public static class Patch_HealthCardUtility_DrawHediffListing
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            // 重置行数计数器，保证每帧交替背景重新计算
            Patch_HealthCardUtility_DrawHediffRow.ResetRowCount();
        }

        [HarmonyPostfix]
        public static void Postfix(Rect rect, Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return;

            float bleedRate = pawn.health.hediffSet.BleedRateTotal;
            if (bleedRate > 0.01f)
            {
                // 绘制现代急症失血死亡警告横幅 (高 28px)
                // 横幅背景使用完全不透明的暗红色，以此完全覆盖原版单调的文字
                Rect bannerRect = new Rect(rect.x, rect.yMax - 28f, rect.width, 28f);
                Widgets.DrawBoxSolid(bannerRect, new Color(0.12f, 0.02f, 0.02f, 1.0f));
                Widgets.DrawBoxSolidWithOutline(bannerRect, new Color(0.12f, 0.02f, 0.02f, 1.0f), new Color(0.5f, 0.1f, 0.1f, 0.4f));

                // 绘制闪烁的红色血滴图标
                Texture2D bleedIcon = Patch_HealthCardUtility_UI.BleedIcon;
                if (bleedIcon != null)
                {
                    bool isBlink = (Time.realtimeSinceStartup % 1.0f) < 0.5f;
                    GUI.color = isBlink ? Color.red : new Color(0.5f, 0.1f, 0.1f);
                    Widgets.DrawTextureFitted(new Rect(bannerRect.x + 6f, bannerRect.y + 5f, 16f, 16f), bleedIcon, 1f);
                    GUI.color = Color.white;
                }

                // 格式化失血死亡时间
                int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                string text = "BleedingRate".Translate() + ": " + bleedRate.ToStringPercent() + "/d";
                if (ticks > 0 && ticks < 150000) // 小于60小时则显示倒计时
                {
                    text += " (" + "DeathIn".Translate(ticks.ToStringTicksToPeriod(true, false, false, false, true)) + ")";
                }

                // 绘制小字体红色警示字样
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.95f, 0.35f, 0.35f);
                Widgets.Label(new Rect(bannerRect.x + 28f, bannerRect.y + 5f, bannerRect.width - 34f, 20f), text);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // 绘制底部失血死亡生存时间滑块进度条 (小于24小时才显示)
                if (ticks > 0 && ticks < 60000)
                {
                    float pct = Mathf.Clamp01((float)ticks / 60000f);
                    Rect barRect = new Rect(bannerRect.x + 4f, bannerRect.yMax - 3f, bannerRect.width - 8f, 2f);
                    Widgets.DrawBoxSolid(barRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

                    Color barColor = Color.yellow;
                    if (pct <= 0.25f) barColor = Color.red;
                    else if (pct <= 0.5f) barColor = new Color(1.0f, 0.5f, 0.0f); // 橙色

                    Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), barColor);
                }
            }
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "DrawHediffRow")]
    public static class Patch_HealthCardUtility_DrawHediffRow
    {
        private static int rowCount = 0;

        public static void ResetRowCount()
        {
            rowCount = 0;
        }

        public static int GetAndIncrementRowCount()
        {
            return rowCount++;
        }

        [HarmonyPrefix]
        public static void Prefix(Rect rect, Pawn pawn, IEnumerable<Hediff> diffs, ref float curY, out float __state)
        {
            __state = curY;

            if (diffs == null || !diffs.Any()) return;

            float rowHeight = 20f; 
            Rect rowRect = new Rect(0f, curY, rect.width, rowHeight);

            int rowIndex = GetAndIncrementRowCount();
            if (rowIndex % 2 == 1)
            {
                if (Patch_HealthCardUtility_UI.isDrawingHealthTab && Patch_HealthCardUtility_UI.useDarkMode)
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.04f));
                }
                else
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(0f, 0f, 0f, 0.05f));
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Rect rect, Pawn pawn, IEnumerable<Hediff> diffs, ref float curY, float rowLeftPad, float __state)
        {
            if (diffs == null || !diffs.Any()) return;

            float rowHeight = curY - __state;

            // 1. 判定是否为致命危机条目
            bool isCritical = false;
            foreach (var h in diffs)
            {
                if (h == null) continue;
                if (h.def == EE_DefOf.EE_MyocardialInfarction ||
                    h.def == EE_DefOf.EE_Shock ||
                    h.def == EE_DefOf.MassiveBleeding ||
                    h.def == EE_DefOf.MultipleOrganFailure ||
                    h.def == EE_DefOf.EE_Sepsis ||
                    h.def == EE_DefOf.EE_BiologicalDeathTimer ||
                    (h.def == EE_DefOf.EE_Pneumothorax && !(h is Hediff_Pneumothorax p && p.isDecompressed))
                   )
                {
                    isCritical = true;
                    break;
                }
                if (h.BleedRate > 0.3f)
                {
                    isCritical = true;
                    break;
                }
            }

            if (isCritical)
            {
                // 使用低透明度红光在 Postfix 覆盖绘制，可获得绝对精准的上下对齐高度 (rowHeight)
                float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 2f, 1f);
                float alpha = Mathf.Lerp(0.04f, 0.14f, pulse);
                Rect rowRect = new Rect(0f, __state, rect.width, rowHeight);
                Widgets.DrawBoxSolid(rowRect, new Color(1.0f, 0.1f, 0.1f, alpha));

                // 配合 rowLeftPad 决定红线的位置，防止重叠文字 (通常向左缩进并空出 6px)
                float indicatorX = rect.x + rowLeftPad - 6f;
                Rect indicatorRect = new Rect(indicatorX, __state + 1f, 3f, rowHeight - 2f);
                Widgets.DrawBoxSolid(indicatorRect, Color.red);
            }

            // 3. 绘制胶囊药丸徽章 (Pill Badges)
            foreach (var h in diffs)
            {
                if (h == null) continue;

                if (h is Hediff_Fracture fracture)
                {
                    string badgeText = "";
                    Color badgeColor = Color.white;

                    if (fracture.isInternallyFixed)
                    {
                        badgeText = "钢板内固定";
                        badgeColor = new Color(0.0f, 1.0f, 1.0f); // 青蓝色
                    }
                    else if (fracture.isCasted)
                    {
                        badgeText = "石膏固定";
                        badgeColor = new Color(0.25f, 0.88f, 0.82f); // 亮青色
                    }
                    else if (fracture.isStrictBedrest)
                    {
                        badgeText = "正骨静卧";
                        badgeColor = new Color(1.0f, 0.65f, 0.0f); // 橙黄色
                    }
                    else if (fracture.isSplinted)
                    {
                        badgeText = "骨折环固定";
                        badgeColor = new Color(0.85f, 0.44f, 0.84f); // 浅紫色
                    }
                    else
                    {
                        badgeText = "未固定";
                        badgeColor = new Color(1.0f, 0.39f, 0.28f); // 亮红色
                    }

                    if (!string.IsNullOrEmpty(badgeText))
                    {
                        if (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName == "English")
                        {
                            if (fracture.isInternallyFixed) badgeText = "Internally Fixed";
                            else if (fracture.isCasted) badgeText = "Casted";
                            else if (fracture.isStrictBedrest) badgeText = "Bedrest";
                            else if (fracture.isSplinted) badgeText = "Splinted";
                            else badgeText = "Unfixed";
                        }

                        Text.Font = GameFont.Small;
                        float labelWidth = Text.CalcSize(fracture.Label).x; // 因为 isDrawingHealthTab 为 true，此处返回不带括弧的干净 label
                        
                        float badgeX = rect.x + rowLeftPad + labelWidth + 8f;
                        float badgeY = __state + (rowHeight - 16f) / 2f;
                        
                        DrawPillBadge(badgeX, badgeY, badgeText, badgeColor);
                    }
                }
                else if (h is Hediff_Pneumothorax pneumo && pneumo.isDecompressed)
                {
                    string badgeText = "已减压";
                    Color badgeColor = new Color(0.0f, 1.0f, 0.5f); // 翠绿色

                    if (LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName == "English")
                    {
                        badgeText = "Decompressed";
                    }

                    Text.Font = GameFont.Small;
                    float labelWidth = Text.CalcSize(pneumo.Label).x;
                    
                    float badgeX = rect.x + rowLeftPad + labelWidth + 8f;
                    float badgeY = __state + (rowHeight - 16f) / 2f;
                    
                    DrawPillBadge(badgeX, badgeY, badgeText, badgeColor);
                }
            }
        }

        private static void DrawPillBadge(float x, float y, string text, Color color)
        {
            Text.Font = GameFont.Tiny;
            Vector2 size = Text.CalcSize(text);
            float badgeW = size.x + 8f;
            float badgeH = size.y + 2f;
            Rect badgeRect = new Rect(x, y + 1f, badgeW, badgeH);

            // 绘制 15% 不透明度的胶囊背景
            Widgets.DrawBoxSolid(badgeRect, new Color(color.r, color.g, color.b, 0.15f));

            // 绘制 50% 不透明度的边框
            GUI.color = new Color(color.r, color.g, color.b, 0.5f);
            Widgets.DrawBox(badgeRect, 1);
            GUI.color = Color.white;

            // 居中写入文本
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(badgeRect, text);
            GUI.color = Color.white;
            Text.Anchor = oldAnchor;
            Text.Font = GameFont.Small;
        }
    }}
