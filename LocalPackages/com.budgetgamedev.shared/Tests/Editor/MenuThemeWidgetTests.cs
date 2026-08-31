using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the small UI primitives every menu surface is built from: panels,
    /// labels, buttons and the card shadow.
    /// </summary>
    public sealed class MenuThemeWidgetTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void CreateRectParentsANewRectWithoutRescalingIt()
        {
            RectTransform parent = Root();

            RectTransform child = MenuTheme.CreateRect("Child", parent);

            Assert.That(child.name, Is.EqualTo("Child"));
            Assert.That(child.parent, Is.SameAs(parent));
            Assert.That(child.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void CreatePanelPaintsABackgroundThatSwallowsNoInput()
        {
            RectTransform panel = MenuTheme.CreatePanel("Panel", Root(), MenuTheme.CardSurface);

            Image image = panel.GetComponent<Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color, Is.EqualTo(MenuTheme.CardSurface));
            Assert.That(
                image.raycastTarget,
                Is.False,
                "Decoration must not steal clicks from the buttons above it."
            );
        }

        [Test]
        public void CreateTextAppliesTheMenuLabelStyle()
        {
            TMP_Text text = MenuTheme.CreateText(
                "Label",
                Root(),
                "PLAY",
                42f,
                MenuTheme.OnSurface,
                null
            );

            Assert.That(text.text, Is.EqualTo("PLAY"));
            Assert.That(text.fontSize, Is.EqualTo(42f).Within(0.001f));
            Assert.That(text.color, Is.EqualTo(MenuTheme.OnSurface));
            Assert.That(text.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(text.fontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(text.raycastTarget, Is.False);
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(text.characterSpacing, Is.EqualTo(2f).Within(0.001f));
            Assert.That(
                text.enableAutoSizing,
                Is.False,
                "Menu labels are sized by the caller, not by TMP."
            );
        }

        [Test]
        public void StyleTextOnlyReplacesTheFontWhenOneIsOffered()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Assert.That(font, Is.Not.Null, "The project ships a default TMP font asset.");

            TMP_Text label = MenuTheme.CreateText("Label", Root(), "A", 20f, Color.white, null);
            TMP_FontAsset before = label.font;

            MenuTheme.StyleText(label, "B", 24f, Color.red, null);
            Assert.That(
                label.font,
                Is.SameAs(before),
                "A null font must leave the label's own font in place."
            );
            Assert.That(label.text, Is.EqualTo("B"));

            MenuTheme.StyleText(label, "C", 24f, Color.red, font);
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.text, Is.EqualTo("C"));
        }

        [Test]
        public void StylingANullButtonIsHarmless()
        {
            Assert.DoesNotThrow(() => MenuTheme.StyleButton(null, true, null));
        }

        [Test]
        public void APrimaryButtonWearsTheAccentFillAndASecondaryOneTheSurface()
        {
            Button primary = NewButton(withImage: true, withLabel: true);
            Button secondary = NewButton(withImage: true, withLabel: true);

            MenuTheme.StyleButton(primary, true, null);
            MenuTheme.StyleButton(secondary, false, null);

            Assert.That(primary.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(primary.colors.normalColor, Is.EqualTo(MenuTheme.Primary));
            Assert.That(secondary.colors.normalColor, Is.EqualTo(MenuTheme.SurfaceVariant));
            Assert.That(primary.colors.selectedColor, Is.EqualTo(MenuTheme.PrimaryHover));
            Assert.That(
                primary.colors.highlightedColor.grayscale,
                Is.GreaterThan(primary.colors.normalColor.grayscale)
            );
            Assert.That(
                primary.colors.pressedColor.grayscale,
                Is.LessThan(primary.colors.normalColor.grayscale)
            );
            Assert.That(
                secondary.colors.highlightedColor.grayscale,
                Is.GreaterThan(secondary.colors.normalColor.grayscale)
            );
            Assert.That(
                secondary.colors.pressedColor.grayscale,
                Is.LessThan(secondary.colors.normalColor.grayscale)
            );
            Assert.That(primary.colors.disabledColor.a, Is.LessThan(1f));
            Assert.That(primary.colors.fadeDuration, Is.GreaterThan(0f));
        }

        [Test]
        public void ThePrimaryButtonSitsOnADeeperShadowThanASecondaryOne()
        {
            Button primary = NewButton(withImage: true, withLabel: true);
            Button secondary = NewButton(withImage: true, withLabel: true);

            MenuTheme.StyleButton(primary, true, null);
            MenuTheme.StyleButton(secondary, false, null);

            Shadow deep = primary.GetComponent<Shadow>();
            Shadow shallow = secondary.GetComponent<Shadow>();
            Assert.That(deep.effectColor.a, Is.GreaterThan(shallow.effectColor.a));
            Assert.That(deep.effectDistance.y, Is.LessThan(shallow.effectDistance.y));
            Assert.That(deep.effectDistance.y, Is.LessThan(0f), "The shadow falls downwards.");
            Assert.That(deep.useGraphicAlpha, Is.True);
        }

        [Test]
        public void StylingResetsTheFillToAFlatWhiteTintTheColourBlockCanDrive()
        {
            Button button = NewButton(withImage: true, withLabel: true);
            Image fill = button.GetComponent<Image>();
            fill.type = Image.Type.Sliced;
            fill.color = Color.magenta;

            MenuTheme.StyleButton(button, true, null);

            Assert.That(fill.sprite, Is.Null);
            Assert.That(fill.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(
                fill.color,
                Is.EqualTo(Color.white),
                "The tint comes from the ColorBlock, so the fill must be neutral."
            );
        }

        [Test]
        public void RestylingAButtonReusesTheShadowItAlreadyHas()
        {
            Button button = NewButton(withImage: true, withLabel: true);

            MenuTheme.StyleButton(button, true, null);
            Shadow first = button.GetComponent<Shadow>();
            MenuTheme.StyleButton(button, false, null);

            Assert.That(button.GetComponents<Shadow>().Length, Is.EqualTo(1));
            Assert.That(button.GetComponent<Shadow>(), Is.SameAs(first));
        }

        [Test]
        public void AButtonWithNeitherFillNorLabelStillGetsItsColoursAndShadow()
        {
            Button bare = NewButton(withImage: false, withLabel: false);

            MenuTheme.StyleButton(bare, false, null);

            Assert.That(bare.GetComponent<Image>(), Is.Null);
            Assert.That(bare.GetComponentInChildren<TMP_Text>(true), Is.Null);
            Assert.That(bare.GetComponent<Shadow>(), Is.Not.Null);
            Assert.That(bare.colors.normalColor, Is.EqualTo(MenuTheme.SurfaceVariant));
        }

        [Test]
        public void TheButtonLabelIsCentredBoldAndInsetFromTheEdges()
        {
            Button button = NewButton(withImage: true, withLabel: true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            MenuTheme.StyleButton(button, true, TMP_Settings.defaultFontAsset);

            Assert.That(label.font, Is.SameAs(TMP_Settings.defaultFontAsset));
            Assert.That(label.color, Is.EqualTo(MenuTheme.OnSurface));
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(label.fontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(label.characterSpacing, Is.EqualTo(2f).Within(0.001f));
            Assert.That(label.enableAutoSizing, Is.False);
            Assert.That(label.margin, Is.EqualTo(new Vector4(24f, 0f, 24f, 0f)));
            Assert.That(label.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(label.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void ACardShadowFallsBelowTheCard()
        {
            RectTransform card = MenuTheme.CreatePanel("Card", Root(), MenuTheme.CardSurface);

            MenuTheme.AddCardShadow(card);

            Shadow shadow = card.GetComponent<Shadow>();
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.effectDistance.x, Is.EqualTo(0f));
            Assert.That(shadow.effectDistance.y, Is.LessThan(0f));
            Assert.That(shadow.effectColor.a, Is.GreaterThan(0f));
            Assert.That(shadow.useGraphicAlpha, Is.True);
        }

        private RectTransform Root()
        {
            var host = new GameObject("Root", typeof(RectTransform));
            _created.Add(host);
            return host.GetComponent<RectTransform>();
        }

        private Button NewButton(bool withImage, bool withLabel)
        {
            RectTransform host = Root();
            if (withImage)
                host.gameObject.AddComponent<Image>();

            Button button = host.gameObject.AddComponent<Button>();
            if (withLabel)
            {
                var labelHost = new GameObject("Label", typeof(RectTransform));
                labelHost.transform.SetParent(host, false);
                labelHost.AddComponent<TextMeshProUGUI>();
            }

            return button;
        }
    }
}
