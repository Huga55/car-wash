using NUnit.Framework;
using UnityEngine;

namespace LittleGarage.Tests
{
    /// <summary>
    /// Pure-data checks for the shared UI layout contract. These verify the
    /// configuration used by UIManager, not rendered Game View layout.
    /// </summary>
    public class UiLayoutTests
    {
        [Test]
        public void LayoutContract_UsesDeliberatePortraitReference()
        {
            Assert.That(UiLayoutMetrics.ReferenceWidth, Is.EqualTo(1080f).Within(0.0001f));
            Assert.That(UiLayoutMetrics.ReferenceHeight, Is.EqualTo(1920f).Within(0.0001f));
            Assert.AreEqual(new Vector2(1080f, 1920f), UiLayoutMetrics.ReferenceResolution);
        }

        [Test]
        public void LayoutContract_UsesOneDocumentedScalerMatchForEveryAspect()
        {
            Assert.That(UiLayoutMetrics.ScalerMatch, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(UiLayoutMetrics.ScalerMatchForAspect(16f / 9f), Is.EqualTo(UiLayoutMetrics.ScalerMatch).Within(0.0001f));
            Assert.That(UiLayoutMetrics.ScalerMatchForAspect(16f / 10f), Is.EqualTo(UiLayoutMetrics.ScalerMatch).Within(0.0001f));
            Assert.That(UiLayoutMetrics.ScalerMatchForAspect(4f / 3f), Is.EqualTo(UiLayoutMetrics.ScalerMatch).Within(0.0001f));
            Assert.That(UiLayoutMetrics.ScalerMatchForAspect(3f / 4f), Is.EqualTo(UiLayoutMetrics.ScalerMatch).Within(0.0001f));
        }

        [Test]
        public void LayoutContract_HudBandsAreSeparated()
        {
            Assert.IsTrue(UiLayoutMetrics.HudBandsAreSeparated(),
                "Top bar, instruction, hint and bottom bar must not share anchor boundaries.");
        }

        [Test]
        public void LayoutContract_GarageContentIsNarrowerInLandscape()
        {
            float portrait = UiLayoutMetrics.GarageContentSideInset01(3f / 4f);
            float landscape = UiLayoutMetrics.GarageContentSideInset01(16f / 9f);
            Assert.Greater(landscape, portrait,
                "Landscape task cards need larger side insets than portrait cards.");
        }

        [Test]
        public void LayoutContract_ToolButtonsKeepMinimumTouchTarget()
        {
            Assert.GreaterOrEqual(UiLayoutMetrics.ToolButtonPreferredHeight, UiLayoutMetrics.MinTouchTargetHeight);
            Assert.GreaterOrEqual(UiLayoutMetrics.MinTouchTargetHeight, 96f);
        }
    }
}
