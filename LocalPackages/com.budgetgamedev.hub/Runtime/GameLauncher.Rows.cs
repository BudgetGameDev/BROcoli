using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Hub
{
    public sealed partial class GameLauncher
    {
        private const float RowHeight = 76f;

        /// <summary>
        /// Builds one row. A row shows the game's own name, icon and blurb, which
        /// is the only branding the launcher carries.
        /// </summary>
        private GameEntry CreateEntry(GameDefinition game, int index)
        {
            RectTransform row = CreateRect(game.Id, listContent);
            row.sizeDelta = new Vector2(0f, RowHeight);
            row.gameObject.AddComponent<LayoutElement>().minHeight = RowHeight;

            Image background = AddImage(row, RowIdle);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.interactable = game.IsPlayable;
            button.onClick.AddListener(() => Select(index));

            float textLeft = 16f;
            if (game.Icon != null)
            {
                RectTransform icon = CreateRect("Icon", row);
                icon.anchorMin = new Vector2(0f, 0.5f);
                icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(0f, 0.5f);
                icon.anchoredPosition = new Vector2(12f, 0f);
                icon.sizeDelta = new Vector2(52f, 52f);
                AddImage(icon, Color.white).sprite = game.Icon;
                textLeft = 76f;
            }

            RectTransform nameRect = CreateRect("Name", row);
            Stretch(nameRect, new Vector2(0f, 1f), new Vector2(1f, 1f));
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -12f);
            nameRect.offsetMin = new Vector2(textLeft, nameRect.offsetMin.y);
            nameRect.offsetMax = new Vector2(-16f, nameRect.offsetMax.y);
            nameRect.sizeDelta = new Vector2(nameRect.sizeDelta.x, 26f);
            Text label = AddText(nameRect, game.DisplayName, 20, Ink, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;

            string blurb = game.IsPlayable
                ? game.Description
                : "Unavailable: no main menu scene is configured.";
            if (!string.IsNullOrWhiteSpace(blurb))
            {
                RectTransform blurbRect = CreateRect("Description", row);
                Stretch(blurbRect, new Vector2(0f, 0f), new Vector2(1f, 0f));
                blurbRect.pivot = new Vector2(0.5f, 0f);
                blurbRect.anchoredPosition = new Vector2(0f, 12f);
                blurbRect.offsetMin = new Vector2(textLeft, blurbRect.offsetMin.y);
                blurbRect.offsetMax = new Vector2(-16f, blurbRect.offsetMax.y);
                blurbRect.sizeDelta = new Vector2(blurbRect.sizeDelta.x, 24f);
                Text description = AddText(blurbRect, blurb, 15, InkMuted, TextAnchor.MiddleLeft);
                description.horizontalOverflow = HorizontalWrapMode.Wrap;
                description.verticalOverflow = VerticalWrapMode.Truncate;
            }

            return new GameEntry
            {
                Game = game,
                Button = button,
                Background = background,
                Label = label,
            };
        }

        private static void ApplyRowStyle(GameEntry entry, bool selected)
        {
            entry.Background.color = selected ? RowSelected : RowIdle;
            entry.Label.color = entry.Game.IsPlayable ? Ink : InkMuted;
        }
    }
}
