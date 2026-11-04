using com.mjgame.nmd.nkl.editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.tests
{
    /// <summary>Shine, motion scale and tag validation.</summary>
    public class FeatureTests
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
            TextFXSettings.SetPlayerMotionScale(-1f);
            TextFXSettings.Set(null);
        }

        TextMeshProUGUI CreateText(string text)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(1000f, 200f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = new Color32(100, 100, 100, 255);
            return tmp;
        }

        static void SetTag(TextEffect effect, string tag)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("_tag").stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Color32 Corner(TMP_Text text, int charIndex, int corner) =>
            text.textInfo.meshInfo[0].colors32[text.textInfo.characterInfo[charIndex].vertexIndex + corner];

        [Test]
        public void Shine_LightsUpOnlyNearTheBand()
        {
            TextMeshProUGUI text = CreateText("ABCDEFGHIJ");
            var shine = text.gameObject.AddComponent<ShineEffect>();
            SetTag(shine, "");
            shine.slant = 0f;
            shine.width = 0.2f;
            shine.duration = 1f;
            shine.interval = 1f;
            shine.intensity = 1f;
            text.ForceMeshUpdate();
            var fx = text.GetComponent<TextFX>();

            shine.Play();
            fx.Tick(0.5f); // band center in the middle of the text

            Color32 middle = Corner(text, 5, 0);
            Color32 first = Corner(text, 0, 0);
            Assert.Greater(middle.r, 100, "characters under the band get brighter");
            Assert.AreEqual(100, first.r, "characters far from the band keep their color");
            Assert.AreEqual(255, middle.a, "alpha is kept");

            fx.Tick(0.9f); // t = 1.4: in the pause between sweeps
            Assert.AreEqual(100, Corner(text, 5, 0).r, "no band during the pause");
        }

        [Test]
        public void Shine_WithoutLoop_EndsAfterOneSweep()
        {
            TextMeshProUGUI text = CreateText("Claim");
            var shine = text.gameObject.AddComponent<ShineEffect>();
            SetTag(shine, "");
            shine.loop = false;
            shine.duration = 0.5f;
            text.ForceMeshUpdate();
            shine.Play();
            text.GetComponent<TextFX>().Tick(0.6f);
            Assert.IsFalse(shine.IsPlaying);
        }

        [Test]
        public void ShineTag_IsBuiltIn()
        {
            TextMeshProUGUI text = CreateText("");
            text.gameObject.AddComponent<TextFX>();
            text.text = "<shine>Claim</shine>";
            text.ForceMeshUpdate(false, true);
            Assert.AreEqual("Claim", text.GetParsedText());
            Assert.IsNotNull(text.GetComponent<ShineEffect>());
        }

        float WaveOffset(float motion)
        {
            TextMeshProUGUI text = CreateText("A");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            SetTag(wave, "");
            wave.amplitude = 10f;
            wave.speed = Mathf.PI / 2f;
            wave.spacing = 0f;
            text.ForceMeshUpdate();
            float y0 = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[0].vertexIndex].y;

            TextFXSettings.SetPlayerMotionScale(motion);
            wave.Play();
            text.GetComponent<TextFX>().Tick(1f); // sin(π/2) = 1
            float y1 = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[0].vertexIndex].y;
            Object.DestroyImmediate(text.gameObject);
            return y1 - y0;
        }

        [Test]
        public void MotionScale_ScalesMovement()
        {
            Assert.AreEqual(10f, WaveOffset(1f), 0.01f);
            Assert.AreEqual(5f, WaveOffset(0.5f), 0.01f);
            Assert.AreEqual(0f, WaveOffset(0f), 0.01f, "reduced motion: no movement");
        }

        [Test]
        public void MotionScale_PlayerChoiceWinsOverQualityOverride()
        {
            var settings = ScriptableObject.CreateInstance<TextFXSettings>();
            settings.motionScale = 1f;
            settings.maxUpdatesPerSecond = 0;
            settings.qualityOverrides = new[]
            {
                new TextFXSettings.QualityOverride
                {
                    qualityLevel = QualitySettings.GetQualityLevel(), motionScale = 0.25f, maxUpdatesPerSecond = 30,
                },
            };
            TextFXSettings.Set(settings);

            Assert.AreEqual(0.25f, TextFXSettings.Instance.MotionScale, 1e-4f, "current quality level uses its override");
            Assert.AreEqual(30, TextFXSettings.Instance.MaxUpdatesPerSecond);
            TextFXSettings.SetPlayerMotionScale(0f);
            Assert.AreEqual(0f, TextFXSettings.Instance.MotionScale, "player choice wins");
            TextFXSettings.SetPlayerMotionScale(-1f);
            Assert.AreEqual(0.25f, TextFXSettings.Instance.MotionScale, 1e-4f, "cleared");
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Validator_FindsTyposOnly()
        {
            TextMeshProUGUI text = CreateText("");
            var issues = TextTagValidator.Find(
                "<b>Hi</b> <#ff0000>red</color> <wavee>x</wavee> <gold>y</gold> <nofx>z</nofx> <sprite=0> " +
                "<noparse><anything></noparse>", text.gameObject);

            Assert.AreEqual(1, issues.Count, "each unknown name is reported once (opening and closing tag)");
            Assert.AreEqual("<wavee>", issues[0].Tag);
            Assert.AreEqual("did you mean <wave>?", issues[0].Hint);
        }

        [Test]
        public void Validator_TypewriterTags_NeedTheTypewriter()
        {
            TextMeshProUGUI text = CreateText("");
            var issues = TextTagValidator.Find("Wait<p=0.5> now", text.gameObject);
            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("Typewriter", issues[0].Hint);

            text.gameObject.AddComponent<TypewriterEffect>();
            Assert.AreEqual(0, TextTagValidator.Find("Wait<p=0.5> now", text.gameObject).Count);
        }
    }
}
