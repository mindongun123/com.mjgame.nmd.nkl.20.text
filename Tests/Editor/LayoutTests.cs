using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl.tests
{
    /// <summary>Static layouts: arc and curve.</summary>
    public class LayoutTests
    {
        GameObject _canvas;
        bool _autoPreview;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
            _autoPreview = TextEffect.AutoPreview;
            TextEffect.AutoPreview = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            TextEffect.AutoPreview = _autoPreview;
        }

        TextMeshProUGUI CreateText(string text)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(1000f, 200f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = text;
            return tmp;
        }

        // Middle of the bottom edge of a character quad.
        static Vector2 Foot(TMP_Text text, int c)
        {
            TMP_CharacterInfo ch = text.textInfo.characterInfo[c];
            Vector3[] v = text.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
            return (v[ch.vertexIndex] + v[ch.vertexIndex + 3]) * 0.5f;
        }

        static float Tilt(TMP_Text text, int c)
        {
            TMP_CharacterInfo ch = text.textInfo.characterInfo[c];
            Vector3[] v = text.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
            Vector3 d = v[ch.vertexIndex + 3] - v[ch.vertexIndex];
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        TextMeshProUGUI Layout<T>(string s, System.Action<T> setup) where T : TextLayoutEffect
        {
            TextMeshProUGUI text = CreateText(s);
            text.ForceMeshUpdate();
            var layout = text.gameObject.AddComponent<T>();
            setup?.Invoke(layout);
            layout.Play();
            text.GetComponent<TextFX>().Tick(0f);
            return text;
        }

        [Test]
        public void Arc_BendsUp_Symmetrically()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            float middleY = Foot(text, 2).y;
            float leftY = Foot(text, 0).y;

            var arc = text.gameObject.AddComponent<ArcLayout>();
            arc.Angle = 90f;
            arc.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Assert.Less(Foot(text, 0).y, leftY - 1f, "ends drop below the baseline");
            Assert.AreEqual(Foot(text, 0).y, Foot(text, 4).y, 0.5f, "symmetric");
            Assert.AreEqual(middleY, Foot(text, 2).y, 0.5f, "the middle stays on top of the arc");
            Assert.Greater(Tilt(text, 0), 5f, "left character leans left (counter-clockwise)");
            Assert.Less(Tilt(text, 4), -5f, "right character leans right");
        }

        [Test]
        public void Arc_NegativeAngle_BendsDown()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            float leftY = Foot(text, 0).y;
            var arc = text.gameObject.AddComponent<ArcLayout>();
            arc.Angle = -90f;
            arc.Play();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.Greater(Foot(text, 0).y, leftY + 1f, "ends rise above the baseline");
        }
        [Test]
        public void Arc_InnerLines_KeepTheirSpacing()
        {
            TextMeshProUGUI text = CreateText("ABCDEFG\nABCDEFG");
            text.ForceMeshUpdate();
            int a = 7 + 1, b = 7 + 2; // characters of the second line (after "\n")
            float before = Vector2.Distance(Foot(text, a), Foot(text, b));

            var arc = text.gameObject.AddComponent<ArcLayout>();
            arc.Angle = 120f;
            arc.Play();
            text.GetComponent<TextFX>().Tick(0f);

            float after = Vector2.Distance(Foot(text, a), Foot(text, b));
            Assert.AreEqual(before, after, before * 0.05f, "second line is on its own circle, not squeezed");
        }

        [Test]
        public void Arc_Radius_SmallerBendsMore_AndFollowsDirection()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            float flatY = Foot(text, 0).y;
            var arc = text.gameObject.AddComponent<ArcLayout>();
            var fx = text.GetComponent<TextFX>();
            arc.Play();

            arc.Radius = 2000f;
            text.ForceMeshUpdate();
            fx.Tick(0f);
            float dropBig = flatY - Foot(text, 0).y;

            arc.Radius = 200f;
            text.ForceMeshUpdate();
            fx.Tick(0f);
            float dropSmall = flatY - Foot(text, 0).y;

            Assert.AreEqual(ArcLayout.Size.Radius, arc.SizeBy, "setting Radius switches the mode");
            Assert.Greater(dropBig, 0f);
            Assert.Greater(dropSmall, dropBig * 2f, "smaller radius = stronger bend");

            arc.Direction = ArcLayout.Bend.Down;
            text.ForceMeshUpdate();
            fx.Tick(0f);
            Assert.Greater(Foot(text, 0).y, flatY, "radius mode bends down too");
        }

        static float Height(TMP_Text text, int c)
        {
            TMP_CharacterInfo ch = text.textInfo.characterInfo[c];
            Vector3[] v = text.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
            return v[ch.vertexIndex + 1].y - v[ch.vertexIndex].y;
        }

        [Test]
        public void SizeCurve_BiggerInTheMiddle_AndSpreadsCharacters()
        {
            TextMeshProUGUI text = CreateText("HHHHH");
            text.ForceMeshUpdate();
            float h = Height(text, 2);
            float gap = Foot(text, 3).x - Foot(text, 2).x;

            var line = text.gameObject.AddComponent<LineLayout>();
            line.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 2f), new Keyframe(1f, 1f));
            line.UseSizeCurve = true;
            line.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Assert.Greater(Height(text, 2), h * 1.8f, "middle character about twice as big");
            Assert.Less(Height(text, 0), h * 1.3f, "edges stay close to normal");
            Assert.Greater(Foot(text, 3).x - Foot(text, 2).x, gap * 1.2f, "big characters pushed apart, no overlap");
            // Scaled around the baseline: characters grow upwards, their feet stay (only the glyph padding
            // below the baseline grows a little).
            Assert.AreEqual(Foot(text, 0).y, Foot(text, 2).y, 1f, "all stand on the same baseline");
        }

        static float Top(TMP_Text text, int c)
        {
            TMP_CharacterInfo ch = text.textInfo.characterInfo[c];
            return text.textInfo.meshInfo[ch.materialReferenceIndex].vertices[ch.vertexIndex + 1].y;
        }

        static float Center(TMP_Text text, int c) => (Top(text, c) + Foot(text, c).y) * 0.5f;

        [TestCase(TextLayoutEffect.SizeAlign.Middle)]
        [TestCase(TextLayoutEffect.SizeAlign.Top)]
        public void SizeCurve_Align_LinesUpCentersOrTops(TextLayoutEffect.SizeAlign align)
        {
            TextMeshProUGUI text = CreateText("HHHHH");
            text.ForceMeshUpdate();
            float topBefore = Top(text, 2), centerBefore = Center(text, 2);

            var line = text.gameObject.AddComponent<LineLayout>();
            line.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 2f), new Keyframe(1f, 0.5f));
            line.UseSizeCurve = true;
            line.SizeAlignment = align;
            line.Play();
            text.GetComponent<TextFX>().Tick(0f);

            float growth = (Top(text, 2) - Foot(text, 2).y) - (Top(text, 0) - Foot(text, 0).y);
            Assert.Greater(growth, 0f, "middle bigger than the edges");
            float up = Top(text, 2) - Top(text, 0);         // how far the big one sticks out above
            float down = Foot(text, 0).y - Foot(text, 2).y; // ...and below
            if (align == TextLayoutEffect.SizeAlign.Top)
            {
                Assert.Greater(down, growth * 0.7f, "grows mostly downwards");
                Assert.Less(up, growth * 0.3f, "tops stay roughly on one line");
            }
            else
            {
                Assert.Greater(up, growth * 0.25f, "grows up...");
                Assert.Greater(down, growth * 0.25f, "...and down");
                Assert.AreEqual(centerBefore, Center(text, 2), growth * 0.25f, "middle stays about where it was");
            }
            Assert.Greater(topBefore, Foot(text, 0).y);
        }

        [Test]
        public void LineLayout_WithoutOptions_ChangesNothing()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            Vector2 before = Foot(text, 4);
            var line = text.gameObject.AddComponent<LineLayout>();
            line.Play();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.AreEqual(before.x, Foot(text, 4).x, 0.01f);
            Assert.AreEqual(before.y, Foot(text, 4).y, 0.01f);
        }

        [Test]
        public void Arc_ZeroAngle_ChangesNothing()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            Vector2 before = Foot(text, 0);
            var arc = text.gameObject.AddComponent<ArcLayout>();
            arc.Angle = 0f;
            arc.Play();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.AreEqual(before.x, Foot(text, 0).x, 0.01f);
            Assert.AreEqual(before.y, Foot(text, 0).y, 0.01f);
        }

        [Test]
        public void Curve_LiftsByCurveTimesHeight()
        {
            TextMeshProUGUI text = CreateText("ABCDE");
            text.ForceMeshUpdate();
            float y = Foot(text, 2).y;
            var curve = text.gameObject.AddComponent<CurveLayout>();
            curve.Curve = AnimationCurve.Constant(0f, 1f, 1f);
            curve.Height = 10f;
            curve.RotateCharacters = false;
            curve.Play();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.AreEqual(y + 10f, Foot(text, 2).y, 0.01f);
            Assert.AreEqual(0f, Tilt(text, 2), 0.01f, "upright");
        }

        [Test]
        public void Layout_RunsBeforeAnimations_EvenWhenAddedLater()
        {
            // Arc alone: position of the tilted first character.
            TextMeshProUGUI text = CreateText("ABCDE");
            var wave = text.gameObject.AddComponent<WaveEffect>(); // added first on purpose
            wave.amplitude = 10f;
            wave.speed = Mathf.PI / 2f;
            wave.spacing = 0f;
            var arc = text.gameObject.AddComponent<ArcLayout>();
            arc.Angle = 120f;
            text.ForceMeshUpdate();
            var fx = text.GetComponent<TextFX>();
            arc.Play();
            fx.Tick(0f);
            Vector2 arcOnly = Foot(text, 0);

            // Wave after arc: the character moves straight up by the amplitude. If the arc ran last,
            // the wave offset would be rotated with the character.
            wave.Play();
            fx.Tick(1f); // sin(π/2) = 1
            Vector2 delta = Foot(text, 0) - arcOnly;
            Assert.AreEqual(0f, delta.x, 0.05f);
            Assert.AreEqual(10f, delta.y, 0.05f);
        }
        [Test]
        public void Layout_IsStatic_AndLeavesTheUpdateLoop()
        {
            TextMeshProUGUI text = Layout<ArcLayout>("ABCDE", a => a.Angle = 60f);
            var fx = text.GetComponent<TextFX>();
            Assert.IsTrue(fx.IsPlaying);
            Assert.IsFalse(fx.IsInUpdateLoop, "no per-frame cost");
        }
    }
}
