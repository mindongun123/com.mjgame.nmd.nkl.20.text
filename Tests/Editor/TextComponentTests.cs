using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.nmd.nkl.text.tests
{
    /// <summary>Edit-mode tests of the components on real TMP texts.</summary>
    public class TextComponentTests
    {
        GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        TextMeshProUGUI CreateText(string text)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(1000f, 200f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            return tmp;
        }

        static void SetTag(TextEffect effect, string tag)
        {
            var so = new SerializedObject(effect);
            so.FindProperty("_tag").stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MapSpan(TMP_Text text, TagSpan span, out int first, out int last) =>
            TextFX.MapToCharacters(text.textInfo, span.Start, span.End, out first, out last);

        [Test]
        public void Tags_AreRemoved_AndRecordedAsSpans()
        {
            TextMeshProUGUI text = CreateText("");
            SetTag(text.gameObject.AddComponent<WaveEffect>(), "wave");
            SetTag(text.gameObject.AddComponent<ShakeEffect>(), "Shake");
            var fx = text.GetComponent<TextFX>();

            text.text = "Get <wave>100</wave> <SHAKE>coins</shake> <b>now</b>";
            text.ForceMeshUpdate(false, true);

            Assert.AreEqual("Get 100 coins now", text.GetParsedText());
            Assert.AreEqual(0, text.textInfo.linkCount, "no TMP links are used any more");
            Assert.AreEqual(2, fx.Spans.Count);
            Assert.AreEqual("wave", fx.Spans[0].Name);
            MapSpan(text, fx.Spans[0], out int first, out int last);
            Assert.AreEqual(4, first);
            Assert.AreEqual(7, last);
            Assert.AreEqual("Shake", fx.Spans[1].Name);
            MapSpan(text, fx.Spans[1], out first, out last);
            Assert.AreEqual(8, first);
            Assert.AreEqual(13, last);
        }

        [Test]
        public void UnknownTags_AreLeftToTmp()
        {
            TextMeshProUGUI text = CreateText("");
            SetTag(text.gameObject.AddComponent<WaveEffect>(), "wave");

            text.text = "<color=red>A</color> <pulse>B</pulse>";
            text.ForceMeshUpdate(false, true);

            // <color> is handled by TMP, <pulse> has no effect so it stays as plain text.
            Assert.AreEqual("A <pulse>B</pulse>", text.GetParsedText());
            Assert.AreEqual(0, text.GetComponent<TextFX>().Spans.Count);
        }

        [Test]
        public void Tags_Nest_AndCarryArguments()
        {
            TextMeshProUGUI text = CreateText("");
            SetTag(text.gameObject.AddComponent<WaveEffect>(), "wave");
            var fx = text.GetComponent<TextFX>();

            text.text = "<wave a=10 s='2'>AB<nofx>C</nofx>D</wave>E";
            text.ForceMeshUpdate(false, true);

            Assert.AreEqual("ABCDE", text.GetParsedText());
            Assert.AreEqual("wave", fx.Spans[0].Name);
            Assert.AreEqual("nofx", fx.Spans[1].Name);
            MapSpan(text, fx.Spans[0], out int first, out int last);
            Assert.AreEqual(0, first);
            Assert.AreEqual(4, last, "outer span still covers ABCD although <nofx> is nested");

            TagArgs args = TagArgs.Parse(fx.Spans[0].Args);
            Assert.AreEqual(10f, args.GetFloat("a", 0f));
            Assert.AreEqual(2f, args.GetFloat("s", 0f));
            Assert.AreEqual(7f, TagArgs.Parse("=7").GetFloat("a", "", 0f), "main value");
        }

        [Test]
        public void TagArguments_ChangeTheEffectPerRange()
        {
            TextMeshProUGUI text = CreateText("");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            SetTag(wave, "wave");
            wave.amplitude = 1f;
            wave.speed = 0f; // constant phase: offset = sin(n * spacing) * amplitude
            wave.spacing = 0f;
            text.text = "<wave>A</wave><wave=0>B</wave>";
            text.ForceMeshUpdate(false, true);
            var fx = text.GetComponent<TextFX>();
            float yA = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[0].vertexIndex].y;
            float yB = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[1].vertexIndex].y;

            wave.spacing = Mathf.PI / 2f; // n = 0 → sin 0, so use speed instead
            wave.speed = Mathf.PI / 2f;
            wave.Play();
            fx.Tick(1f); // sin(π/2) = 1

            Assert.AreEqual(yA + 1f, text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[0].vertexIndex].y, 0.01f);
            Assert.AreEqual(yB, text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[1].vertexIndex].y, 0.01f,
                "<wave=0> sets amplitude 0 for that range only");
        }

        GradientEffect SolidRed(TextMeshProUGUI text)
        {
            text.color = Color.white;
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.gradient = TwoColor(Color.red, Color.red);
            return gradient;
        }

        static bool IsRed(TMP_Text text, int charIndex) => VertexColor(text, charIndex, 0).g == 0;

        [Test]
        public void NoFx_ExcludesFromAllEffects()
        {
            TextMeshProUGUI text = CreateText("");
            GradientEffect gradient = SolidRed(text);
            text.text = "AB<nofx>CD</nofx>EF";
            text.ForceMeshUpdate(false, true);
            gradient.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Assert.AreEqual("ABCDEF", text.GetParsedText());
            Assert.IsTrue(IsRed(text, 1));
            Assert.IsFalse(IsRed(text, 2));
            Assert.IsFalse(IsRed(text, 3));
            Assert.IsTrue(IsRed(text, 4));
        }

        [Test]
        public void NoFx_WithList_ExcludesOnlyNamedEffects()
        {
            TextMeshProUGUI text = CreateText("");
            GradientEffect gradient = SolidRed(text);
            text.text = "A<nofx=wave>B</nofx><nofx=gradient,wave>C</nofx>";
            text.ForceMeshUpdate(false, true);
            gradient.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Assert.IsTrue(IsRed(text, 1), "<nofx=wave> does not exclude the gradient");
            Assert.IsFalse(IsRed(text, 2), "<nofx=gradient,...> does");
        }

        [Test]
        public void Sprites_AreColored_UnlessWrappedInNoFx()
        {
            TextMeshProUGUI text = CreateText("");
            GradientEffect gradient = SolidRed(text);
            text.text = "A<sprite=0>B<nofx><sprite=0></nofx>";
            text.ForceMeshUpdate(false, true);
            var sprites = new System.Collections.Generic.List<int>();
            for (int c = 0; c < text.textInfo.characterCount; c++)
                if (text.textInfo.characterInfo[c].elementType == TMP_TextElementType.Sprite) sprites.Add(c);
            if (sprites.Count < 2) Assert.Inconclusive("No default TMP sprite asset in this project.");

            gradient.Play();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.IsTrue(IsRed(text, 0));
            Assert.IsTrue(IsRed(text, sprites[0]), "icons are treated like characters");
            Assert.IsFalse(IsRed(text, sprites[1]), "<nofx> keeps an icon in its own colors");
        }

        [Test]
        public void Typewriter_PauseSpeedAndEventTags()
        {
            TextMeshProUGUI text = CreateText("");
            var typer = text.gameObject.AddComponent<TypewriterEffect>();
            typer.charactersPerSecond = 10f;
            typer.fadeDuration = 0f;
            typer.punctuationPause = 0f;
            var fx = text.GetComponent<TextFX>();
            var events = new System.Collections.Generic.List<string>();
            int revealed = 0;
            typer.onEvent.AddListener(events.Add);
            typer.onCharacterRevealed.AddListener(_ => revealed++);

            text.text = "AB<p=1>C<ev=boom><speed=x100>DEFG</speed>H";
            text.ForceMeshUpdate(false, true);
            Assert.AreEqual("ABCDEFGH", text.GetParsedText());

            typer.Play();
            fx.Tick(0.15f); // A at 0, B at 0.1
            Assert.AreEqual(2, revealed);
            fx.Tick(0.5f); // C waits for the 1 s pause
            Assert.AreEqual(2, revealed);
            fx.Tick(0.6f); // t = 1.25: C revealed at 1.2; D is due at 1.3, its tags already ran
            Assert.AreEqual(3, revealed);
            Assert.Contains("boom", events);
            fx.Tick(0.06f); // t = 1.31
            Assert.GreaterOrEqual(revealed, 7, "x100 speed reveals DEFG almost at once");

            typer.Skip();
            fx.Tick(0f);
            Assert.AreEqual(8, revealed);
            Assert.IsTrue(typer.IsComplete);
        }

        [Test]
        public void Dispatcher_WatchesOnlyAwakeTexts()
        {
            int before = TextChangeDispatcher.WatchedCount;
            var texts = new TextMeshProUGUI[5];
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = CreateText("T" + i);
                SolidRed(texts[i]).Play();
                texts[i].GetComponent<TextFX>().Tick(0f);
            }
            Assert.AreEqual(before + 5, TextChangeDispatcher.WatchedCount);

            texts[0].GetComponent<GradientEffect>().Stop();
            texts[0].GetComponent<TextFX>().Tick(0f);
            Assert.AreEqual(before + 4, TextChangeDispatcher.WatchedCount);

            // A change to one text re-applies only that text.
            texts[1].text = "Changed";
            texts[1].ForceMeshUpdate();
            Assert.IsTrue(IsRed(texts[1], 6));
        }

        [Test]
        public void IdleText_IsNotInUpdateLoop()
        {
            TextMeshProUGUI text = CreateText("Idle");
            text.gameObject.AddComponent<WaveEffect>();
            var fx = text.GetComponent<TextFX>();
            int before = TextFXRunner.ActiveCount;

            // Play On Enable only auto-plays in play mode, so the text is idle here.
            Assert.IsFalse(fx.IsAwake);

            fx.PlayAll();
            Assert.IsTrue(fx.IsAwake);
            Assert.AreEqual(before + 1, TextFXRunner.ActiveCount);

            fx.StopAll();
            fx.Tick(0f);
            Assert.IsFalse(fx.IsAwake);
            Assert.AreEqual(before, TextFXRunner.ActiveCount);
        }

        [Test]
        public void Effect_MovesVertices_AndStopRestoresThem()
        {
            TextMeshProUGUI text = CreateText("Wave");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            wave.amplitude = 10f;
            wave.speed = 1f;
            text.ForceMeshUpdate();
            float original = text.textInfo.meshInfo[0].vertices[0].y;

            wave.Play();
            text.GetComponent<TextFX>().Tick(1f);
            Assert.Greater(Mathf.Abs(text.textInfo.meshInfo[0].vertices[0].y - original), 0.001f);

            wave.Stop();
            text.GetComponent<TextFX>().Tick(0f);
            Assert.AreEqual(original, text.textInfo.meshInfo[0].vertices[0].y, 0.001f);
        }

        [Test]
        public void ResetAll_RestoresImmediately()
        {
            TextMeshProUGUI text = CreateText("Reset");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            text.ForceMeshUpdate();
            float original = text.textInfo.meshInfo[0].vertices[0].y;
            var fx = text.GetComponent<TextFX>();

            wave.Play();
            fx.Tick(0.3f);
            fx.ResetAll();

            Assert.IsFalse(fx.IsPlaying);
            Assert.IsFalse(fx.IsAwake);
            Assert.AreEqual(original, text.textInfo.meshInfo[0].vertices[0].y, 0.001f);
        }

        [Test]
        public void OneShotEffect_FinishesAndSleeps()
        {
            TextMeshProUGUI text = CreateText("Pop");
            var bounce = text.gameObject.AddComponent<BounceInEffect>();
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            bounce.Play();
            fx.Tick(0.01f);
            Assert.IsTrue(bounce.IsPlaying);

            fx.Tick(10f);
            Assert.IsFalse(bounce.IsPlaying);
            fx.Tick(0f);
            Assert.IsFalse(fx.IsAwake);
        }

        [Test]
        public void Typewriter_HidesUnrevealed_AndCompletes()
        {
            TextMeshProUGUI text = CreateText("Hello");
            var typer = text.gameObject.AddComponent<TypewriterEffect>();
            typer.charactersPerSecond = 10f;
            typer.fadeDuration = 0f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();
            int revealed = 0;
            bool completed = false;
            typer.onCharacterRevealed.AddListener(_ => revealed++);
            typer.onComplete.AddListener(() => completed = true);

            typer.Play();
            fx.Tick(0.15f); // chars at t=0 and t=0.1
            Assert.AreEqual(2, revealed);
            TMP_CharacterInfo last = text.textInfo.characterInfo[4];
            Assert.AreEqual(0, text.textInfo.meshInfo[last.materialReferenceIndex].colors32[last.vertexIndex].a);

            typer.Skip();
            fx.Tick(0f);
            Assert.AreEqual(5, revealed);
            Assert.IsTrue(completed);
            Assert.IsTrue(typer.IsComplete);
        }

        static Color32 VertexColor(TMP_Text text, int charIndex, int corner)
        {
            TMP_CharacterInfo c = text.textInfo.characterInfo[charIndex];
            return text.textInfo.meshInfo[c.materialReferenceIndex].colors32[c.vertexIndex + corner];
        }

        static Gradient TwoColor(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        [Test]
        public void StaticGradient_LeavesUpdateLoop_ButStaysApplied()
        {
            TextMeshProUGUI text = CreateText("ABCD");
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.gradient = TwoColor(Color.red, Color.blue);
            gradient.wrap = GradientEffect.Wrap.Clamp;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            gradient.Play();
            fx.Tick(0f);

            Assert.IsTrue(fx.IsAwake, "static effect keeps listening for rebuilds");
            Assert.IsFalse(fx.IsInUpdateLoop, "static effect is not ticked every frame");
            Color32 first = VertexColor(text, 0, 0);
            Color32 last = VertexColor(text, 3, 2);
            Assert.Greater(first.r, first.b, "left side is red");
            Assert.Greater(last.b, last.r, "right side is blue");
        }

        [Test]
        public void StaticGradient_ReappliesWhenTextChanges_AndStopRestores()
        {
            TextMeshProUGUI text = CreateText("AB");
            text.color = Color.white;
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.gradient = TwoColor(Color.green, Color.green);
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();
            gradient.Play();
            fx.Tick(0f);

            text.text = "ABCDEF";
            text.ForceMeshUpdate(); // raises TEXT_CHANGED → re-applied in the same frame
            Color32 c = VertexColor(text, 5, 0);
            Assert.AreEqual(0, c.r, "new characters get the gradient too");
            Assert.AreEqual(255, c.g);

            gradient.Stop();
            fx.Tick(0f);
            Assert.IsFalse(fx.IsAwake);
            Assert.AreEqual(255, VertexColor(text, 5, 0).r, "original white is back");
        }

        [Test]
        public void ScrollingGradient_StaysInUpdateLoop()
        {
            TextMeshProUGUI text = CreateText("Scroll");
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.scrollSpeed = 1f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            gradient.Play();
            fx.Tick(0.1f);
            Assert.IsTrue(fx.IsInUpdateLoop);
            Assert.IsTrue(gradient.IsLooping);
        }

        [Test]
        public void Gradient_PerCharacter_GivesOneColorPerGlyph()
        {
            TextMeshProUGUI text = CreateText("WW");
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.gradient = TwoColor(Color.red, Color.blue);
            gradient.coloring = GradientEffect.Coloring.PerCharacter;
            text.ForceMeshUpdate();
            gradient.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Assert.AreEqual(VertexColor(text, 0, 0), VertexColor(text, 0, 2));
            Assert.AreNotEqual(VertexColor(text, 0, 0), VertexColor(text, 1, 0));
        }

        [Test]
        public void Gradient_Multiply_KeepsAlpha()
        {
            TextMeshProUGUI text = CreateText("A");
            text.color = new Color(1f, 1f, 1f, 0.5f);
            var gradient = text.gameObject.AddComponent<GradientEffect>();
            gradient.gradient = TwoColor(Color.red, Color.red);
            gradient.blend = GradientEffect.Blend.Multiply;
            text.ForceMeshUpdate();
            gradient.Play();
            text.GetComponent<TextFX>().Tick(0f);

            Color32 c = VertexColor(text, 0, 0);
            Assert.AreEqual(255, c.r);
            Assert.AreEqual(0, c.g);
            Assert.AreEqual(128, c.a, 1);
        }

        [Test]
        public void ColorFlash_EndsAndRestores()
        {
            TextMeshProUGUI text = CreateText("Flash");
            text.color = Color.white;
            var flash = text.gameObject.AddComponent<ColorFlashEffect>();
            flash.color = Color.black;
            flash.rate = 1f;
            flash.duration = 1f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            flash.Play();
            fx.Tick(0.5f); // peak of the single flash
            Assert.Less(VertexColor(text, 0, 0).r, 10);

            fx.Tick(1f);
            Assert.IsFalse(flash.IsPlaying);
            fx.Tick(0f);
            Assert.AreEqual(255, VertexColor(text, 0, 0).r);
        }

        [Test]
        public void Counter_FormatsAndJumps()
        {
            TextMeshProUGUI text = CreateText("");
            var counter = text.gameObject.AddComponent<TextCounter>();
            counter.prefix = "$ ";
            counter.SetValue(1234567);
            text.ForceMeshUpdate();
            Assert.AreEqual("$ 1,234,567", text.GetParsedText());

            counter.display = TextCounter.Display.Abbreviated;
            counter.abbreviateFrom = 1000;
            counter.SetValue(15300);
            text.ForceMeshUpdate();
            Assert.AreEqual("$ 15.3k", text.GetParsedText());

            counter.display = TextCounter.Display.Time;
            counter.prefix = "";
            counter.SetValue(75);
            text.ForceMeshUpdate();
            Assert.AreEqual("01:15", text.GetParsedText());
        }

        [Test]
        public void Extensions_SetText()
        {
            TextMeshProUGUI text = CreateText("");
            text.SetNumber(1500, "<b>", "</b>");
            text.ForceMeshUpdate();
            Assert.AreEqual("1,500", text.GetParsedText());

            text.SetTime(3725, TimeFormat.Compact);
            text.ForceMeshUpdate();
            Assert.AreEqual("1h 2m", text.GetParsedText());

            text.SetPercent(0.5);
            text.ForceMeshUpdate();
            Assert.AreEqual("50%", text.GetParsedText());

            text.SetFraction(3, 10, "Lv ");
            text.ForceMeshUpdate();
            Assert.AreEqual("Lv 3/10", text.GetParsedText());
        }
    }
}
