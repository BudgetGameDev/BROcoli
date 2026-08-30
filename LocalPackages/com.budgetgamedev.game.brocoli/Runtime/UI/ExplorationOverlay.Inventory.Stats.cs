using System.Text;
using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ExplorationOverlay
    {
        private RectTransform CreateBackpackSlot(RectTransform parent, int index)
        {
            bool occupied = !string.IsNullOrEmpty(backpackItems[index]);
            RectTransform slot = CreatePanel(
                $"BackpackSlot{index + 1:00}",
                parent,
                occupied ? OccupiedSlot : InventorySlot
            );
            AddInventoryOutline(
                slot.gameObject,
                occupied
                    ? new Color(GearAccent.r, GearAccent.g, GearAccent.b, 0.52f)
                    : new Color(1f, 1f, 1f, 0.1f)
            );
            RegisterInventoryItem(slot, InventoryPreviewLocation.Backpack, index);
            TMP_Text label = CreateText(
                "Label",
                slot,
                occupied ? backpackItems[index] : string.Empty,
                10f,
                occupied ? OnSurface : OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            Stretch(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            backpackLabels.Add(label);
            return slot;
        }

        private static TMP_Text CreateStatColumn(string objectName, RectTransform parent)
        {
            TMP_Text text = CreateText(
                objectName,
                parent,
                string.Empty,
                11f,
                OnSurfaceMuted,
                TMP_Settings.defaultFontAsset
            );
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private void LayoutBackpack(float width, float height, bool compact)
        {
            const int columns = 5;
            const int rows = 4;
            float inset = compact ? 7f : 12f;
            float top = compact ? 54f : 64f;
            float gap = compact ? 4f : 7f;
            float cellWidth = (width - inset * 2f - gap * (columns - 1)) / columns;
            float cellHeight = (height - top - inset - gap * (rows - 1)) / rows;

            for (int i = 0; i < backpackSlots.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                RectTransform slot = backpackSlots[i];
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                slot.pivot = new Vector2(0f, 1f);
                slot.anchoredPosition = new Vector2(
                    inset + column * (cellWidth + gap),
                    -top - row * (cellHeight + gap)
                );
                slot.sizeDelta = new Vector2(cellWidth, cellHeight);
                backpackLabels[i].fontSize = compact ? 6f : Mathf.Clamp(cellWidth / 7f, 8f, 11f);
            }
        }

        private void UpdateInventory()
        {
            PlayerStats stats = PlayerStats.Resolve();
            if (stats == null)
            {
                runSummary.text = "WAITING FOR PLAYER DATA…";
                statsLeft.text = "PLAYER DATA\nUNAVAILABLE";
                statsRight.text = string.Empty;
                return;
            }

            GameStates gameStates = FindAnyObjectByType<GameStates>();
            DungeonManager dungeon = FindAnyObjectByType<DungeonManager>();
            int score = gameStates != null ? gameStates.score : 0;
            int kills = gameStates != null ? gameStates.EnemiesKilled : 0;
            int rooms = dungeon != null ? dungeon.RoomsVisited : 0;
            string survived = GameStates.FormatSurvivalTime(
                gameStates != null ? gameStates.gameTime : 0f
            );
            runSummary.text =
                $"SCORE {score:N0}  ·  ROOMS {rooms}  ·  KILLS {kills}  ·  {survived}";

            var left = new StringBuilder(256);
            left.AppendLine(StatLine("LEVEL", $"{Mathf.RoundToInt(stats.CurrentLevel)}"));
            left.AppendLine(
                StatLine(
                    "HEALTH",
                    $"{Mathf.CeilToInt(stats.CurrentHealth)} / {Mathf.CeilToInt(stats.CurrentMaxHealth)}"
                )
            );
            left.AppendLine(StatLine("DAMAGE", $"{stats.CurrentDamage:0.0}"));
            left.AppendLine(StatLine("ATTACK", $"{stats.CurrentAttackSpeed:0.00}s"));
            left.AppendLine(StatLine("MOVE", $"{stats.CurrentMovementSpeed:0.0}"));
            left.AppendLine(StatLine("SPRAY RANGE", $"{stats.CurrentSprayRange:0.0}"));
            left.AppendLine(StatLine("SPRAY WIDTH", $"{stats.CurrentSprayWidth:0.0}"));
            statsLeft.text = left.ToString();

            var right = new StringBuilder(256);
            right.AppendLine(StatLine("ARMOR", $"{stats.CurrentArmor:0.0}"));
            right.AppendLine(StatLine("DODGE", $"{stats.CurrentDodgeChance:0.#}%"));
            right.AppendLine(StatLine("REGEN", $"{stats.CurrentHealthRegen:0.0}/s"));
            right.AppendLine(StatLine("LIFE STEAL", $"{stats.CurrentLifeSteal:0.#}%"));
            right.AppendLine(StatLine("CRIT", $"{stats.CurrentCritChance:0.#}%"));
            right.AppendLine(StatLine("CRIT DMG", $"{stats.CurrentCritDamage * 100f:0}%"));
            right.AppendLine(StatLine("DETECTION", $"{stats.CurrentDetectionRadius:0.0}"));
            statsRight.text = right.ToString();
        }

        private static string StatLine(string label, string value) =>
            $"<color=#9CB2A2>{label}</color>  <b><color=#F1F4ED>{value}</color></b>";
    }
}
