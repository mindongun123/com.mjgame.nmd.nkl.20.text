using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.tests
{
    /// <summary>Edit-mode tests of the components on real TMP texts.</summary>
    public class TextComponentTests
    {
        GameObject _canvas;
        bool _autoPreview;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
            // Tests start effects themselves.
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

            text.text = "<color=red>A</color> <foo>B</foo>";
            text.ForceMeshUpdate(false, true);

            // <color> is handled by TMP, <foo> is no package tag so it stays as plain text.
            Assert.AreEqual("A <foo>B</foo>", text.GetParsedText());
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
            Assert.AreEqual("$ 1234567", text.GetParsedText(), "Number is never grouped");

            counter.display = TextCounter.Display.Abbreviated;
            counter.abbreviateFrom = 1000;
            counter.separator = ",";
            counter.SetValue(15300);
            text.ForceMeshUpdate();
            Assert.AreEqual("$ 15,3k", text.GetParsedText());

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

        [Test]
        public void Counter_DecimalSeparator()
        {
            TextMeshProUGUI text = CreateText("");
            var counter = text.gameObject.AddComponent<TextCounter>();
            counter.display = TextCounter.Display.Abbreviated;
            counter.prefix = "$ ";

            // One setting: in Abbreviated the separator is the only mark (decimals), no grouping.
            counter.separator = ",";
            counter.SetValue(20500);
            Assert.AreEqual("$ 20,5k", text.text);
            counter.SetValue(9999);
            Assert.AreEqual("$ 9999", text.text);
            counter.SetValue(1000);
            Assert.AreEqual("$ 1000", text.text);
            counter.abbreviateFrom = 1000000;
            counter.SetValue(250000);
            Assert.AreEqual("$ 250000", text.text, "no grouping in Abbreviated");
            counter.abbreviateFrom = 10000;

            // The cases asked for: separator ',', abbreviate from 1000, 3 decimals for small values.
            counter.prefix = "$";
            counter.abbreviateFrom = 1000;
            counter.decimals = 3;
            counter.SetValue(0.199);
            Assert.AreEqual("$0,199", text.text);
            counter.prefix = "";
            counter.suffix = "$";
            counter.SetValue(199);
            Assert.AreEqual("199$", text.text);
            counter.suffix = "";
            counter.SetValue(1999.29292);
            Assert.AreEqual("1,999k", text.text, "one Decimals setting for both forms");
            counter.SetValue(2.5);
            Assert.AreEqual("2,5", text.text, "trailing zeros dropped");
            counter.SetValue(-0.05);
            Assert.AreEqual("-0,05", text.text);
            counter.SetValue(58.77);
            Assert.AreEqual("58,77", text.text);
            counter.decimals = 1;
            counter.SetValue(1999.29292);
            Assert.AreEqual("1,9k", text.text);
            counter.SetValue(58.77);
            Assert.AreEqual("58,7", text.text, "truncated, not rounded");
            counter.prefix = "$ ";
            counter.abbreviateFrom = 10000;

            counter.separator = ".";
            counter.SetValue(20500);
            Assert.AreEqual("$ 20.5k", text.text);

            counter.separator = ",";
            counter.display = TextCounter.Display.Number;
            counter.SetValue(1000);
            Assert.AreEqual("$ 1000", text.text, "Number: full value, no grouping");
            counter.SetValue(1058.6);
            Assert.AreEqual("$ 1058,6", text.text, "Number: separator is the decimal mark");
            counter.SetValue(19999999999);
            Assert.AreEqual("$ 19999999999", text.text);
            counter.separator = ".";
            counter.SetValue(1058.6);
            Assert.AreEqual("$ 1058.6", text.text);
            counter.separator = ",";
            counter.display = TextCounter.Display.Abbreviated;
            counter.SetValue(20000);
            Assert.AreEqual("$ 20k", text.text);

            text.SetDecimal(3.14159, 2, null, null, ',');
            Assert.AreEqual("3,14", text.text);
            text.SetPercent(0.756, 1, null, null, ',');
            Assert.AreEqual("75,6%", text.text);
            text.SetAbbreviated(1.5e21, 1, 1000, null, null, '.', ',');
            Assert.AreEqual("1,5Sx", text.text);
        }

        [Test]
        public void Counter_CountsWithThePrecisionOfItsEnds()
        {
            TextMeshProUGUI text = CreateText("");
            var counter = text.gameObject.AddComponent<TextCounter>();
            counter.separator = ",";
            counter.decimals = 2;
            var step = typeof(TextCounter).GetMethod("Step",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            counter.SetValue(50800);
            counter.Add(20000);
            for (int i = 0; i < 20; i++)
            {
                step.Invoke(counter, new object[] { 0.013f });
                StringAssert.DoesNotContain(",", text.text, "whole numbers count in whole numbers: " + text.text);
            }

            counter.SetValue(0.5);
            counter.CountTo(2.75);
            for (int i = 0; i < 20; i++)
            {
                step.Invoke(counter, new object[] { 0.013f });
                int comma = text.text.IndexOf(',');
                Assert.IsTrue(comma < 0 || text.text.Length - comma - 1 <= 2, "at most 2 decimals: " + text.text);
            }
            counter.Complete();
            Assert.AreEqual("2,75", text.text);
        }

        [Test]
        public void Extensions_StripPackageTags()
        {
            TextMeshProUGUI text = CreateText("");
            SetTag(text.gameObject.AddComponent<WaveEffect>(), "wave");
            var fx = text.GetComponent<TextFX>();

            text.SetNumber(1500, "<wave>", "</wave> coins");
            text.ForceMeshUpdate();
            Assert.AreEqual("1,500 coins", text.GetParsedText());
            Assert.AreEqual(1, fx.Spans.Count);

            // Without tags the old spans must not stay.
            text.SetNumber(7);
            text.ForceMeshUpdate();
            Assert.AreEqual("7", text.GetParsedText());
            Assert.AreEqual(0, fx.Spans.Count);
        }

        [Test]
        public void Runner_AddRemove_KeepsIndices()
        {
            int before = TextFXRunner.ActiveCount;
            var fxs = new TextFX[4];
            for (int i = 0; i < fxs.Length; i++)
            {
                TextMeshProUGUI text = CreateText("T" + i);
                text.gameObject.AddComponent<WaveEffect>();
                fxs[i] = text.GetComponent<TextFX>();
                fxs[i].PlayAll();
            }
            Assert.AreEqual(before + 4, TextFXRunner.ActiveCount);

            // Remove from the middle, then the rest: every one must leave the loop.
            fxs[1].StopAll();
            fxs[1].Tick(0f);
            Assert.AreEqual(before + 3, TextFXRunner.ActiveCount);
            for (int i = 0; i < fxs.Length; i++)
            {
                fxs[i].StopAll();
                fxs[i].Tick(0f);
                Assert.IsFalse(fxs[i].IsInUpdateLoop);
            }
            Assert.AreEqual(before, TextFXRunner.ActiveCount);
        }

        [Test]
        public void Extrude_World_ScalesOffsetAndSitsBehind()
        {
            var go = new GameObject("World");
            var world = go.AddComponent<TextMeshPro>();
            world.text = "Hi";
            var extrude = go.AddComponent<TextExtrude>();
            extrude.offset = new Vector2(0f, -10f);
            var layerGo = new GameObject("Layer");
            var layer = layerGo.AddComponent<TextMeshPro>();
            var so = new SerializedObject(extrude);
            so.FindProperty("_layer").objectReferenceValue = layer;
            so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                extrude.Sync();
                float scale = TextFXSettings.Instance.worldUnitScale;
                Assert.AreEqual(-10f * scale, layer.transform.localPosition.y, 1e-4f);
                Assert.Greater(layer.transform.localPosition.z, 0f);
                Assert.Less(layer.sortingOrder, world.sortingOrder);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(layerGo);
            }
        }

        static float VertexY(TMP_Text text, int charIndex) =>
            text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[charIndex].vertexIndex].y;

        [Test]
        public void BuiltInTag_WorksWithoutAddingAnEffect()
        {
            TextMeshProUGUI text = CreateText("");
            var fx = text.gameObject.AddComponent<TextFX>();
            text.text = "A<wave>B</wave>";
            text.ForceMeshUpdate(false, true);

            Assert.AreEqual("AB", text.GetParsedText());
            var wave = text.GetComponent<WaveEffect>();
            Assert.IsNotNull(wave, "a hidden WaveEffect is created");
            Assert.IsTrue(wave.IsAuto);
            Assert.IsTrue((wave.hideFlags & HideFlags.DontSave) != 0, "never saved into the scene");

            text.text = "<wave>A</wave><wave>B</wave>";
            text.ForceMeshUpdate(false, true);
            Assert.AreEqual(1, text.GetComponents<WaveEffect>().Length, "reused by later texts");
        }

        [Test]
        public void BuiltInTag_Animates()
        {
            TextMeshProUGUI text = CreateText("");
            var fx = text.gameObject.AddComponent<TextFX>();
            text.text = "A<shake=20>B</shake>";
            text.ForceMeshUpdate(false, true);
            float yA = VertexY(text, 0), yB = VertexY(text, 1);
            float xB = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[1].vertexIndex].x;

            text.GetComponent<ShakeEffect>().Play();
            fx.Tick(0.1f);

            Assert.AreEqual(yA, VertexY(text, 0), 0.001f, "outside the tag");
            float dx = text.textInfo.meshInfo[0].vertices[text.textInfo.characterInfo[1].vertexIndex].x - xB;
            Assert.Greater(Mathf.Abs(dx) + Mathf.Abs(VertexY(text, 1) - yB), 0.01f);
        }

        [Test]
        public void HandAddedEffect_OverridesBuiltInTag()
        {
            TextMeshProUGUI text = CreateText("");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            SetTag(wave, "wave");
            wave.amplitude = 0f; // this item: no movement
            var fx = text.GetComponent<TextFX>();
            text.text = "<wave>A</wave>";
            text.ForceMeshUpdate(false, true);
            float y = VertexY(text, 0);

            Assert.AreEqual(1, text.GetComponents<WaveEffect>().Length, "no hidden copy for a tag the text already has");
            wave.Play();
            fx.Tick(0.3f);
            Assert.AreEqual(y, VertexY(text, 0), 0.001f);
        }

        [Test]
        public void PresetTag_ComesFromThePresetsPrefab()
        {
            TextMeshProUGUI text = CreateText("");
            text.color = Color.white;
            var fx = text.gameObject.AddComponent<TextFX>();
            text.text = "A<gold>B</gold>";
            text.ForceMeshUpdate(false, true);

            var gold = text.GetComponent<GradientEffect>();
            Assert.IsNotNull(gold, "<gold> is defined in Resources/" + TextFXPresets.ResourcePath);
            Assert.AreEqual("gold", gold.TagName);
            fx.Tick(0f); // static: applied once
            Color32 a = VertexColor(text, 0, 0), b = VertexColor(text, 1, 0);
            Assert.AreEqual(new Color32(255, 255, 255, 255), a);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void TextFX_CanBeRemoved_AfterHiddenEffectsAreCleanedUp()
        {
            TextMeshProUGUI text = CreateText("");
            var fx = text.gameObject.AddComponent<TextFX>();
            text.text = "<wave>A</wave>";
            text.ForceMeshUpdate(false, true);
            Assert.IsTrue(text.GetComponent<WaveEffect>().IsAuto);

            foreach (TextEffect effect in text.GetComponents<TextEffect>()) Object.DestroyImmediate(effect);
            Object.DestroyImmediate(fx);
            Assert.IsNull(text.GetComponent<TextFX>());
        }

        [Test]
        public void AutoPreview_PlaysAnimatedEffects_InEditMode()
        {
            TextEffect.AutoPreview = true;
            TextMeshProUGUI text = CreateText("Hi");
            var wave = text.gameObject.AddComponent<WaveEffect>();
            Assert.IsTrue(wave.IsPlaying);

            TextEffect.AutoPreview = false;
            var shake = text.gameObject.AddComponent<ShakeEffect>();
            Assert.IsFalse(shake.IsPlaying);
        }

        [Test]
        public void AnimatedEffect_LeavesUpdateLoop_WhenTextHasNoTag()
        {
            TextMeshProUGUI text = CreateText("");
            var fx = text.gameObject.AddComponent<TextFX>();
            text.text = "<wave>A</wave>";
            text.ForceMeshUpdate(false, true);
            text.GetComponent<WaveEffect>().Play();
            fx.Tick(0f);
            Assert.IsTrue(fx.IsInUpdateLoop);

            text.text = "plain";
            text.ForceMeshUpdate(false, true);
            fx.Tick(0f);
            Assert.IsFalse(fx.IsInUpdateLoop, "nothing to animate: no per-frame cost");

            text.text = "<wave>again</wave>";
            text.ForceMeshUpdate(false, true);
            fx.OnTextChanged(text);
            Assert.IsTrue(fx.IsInUpdateLoop);
        }

        static Vector3 Vertex(TMP_Text text, int charIndex, int corner)
        {
            TMP_CharacterInfo c = text.textInfo.characterInfo[charIndex];
            return text.textInfo.meshInfo[c.materialReferenceIndex].vertices[c.vertexIndex + corner];
        }

        static float Width(TMP_Text text, int charIndex) => Vertex(text, charIndex, 2).x - Vertex(text, charIndex, 0).x;

        [Test]
        public void Pulse_ScalesCharacters_AndStopRestores()
        {
            TextMeshProUGUI text = CreateText("Pulse");
            var pulse = text.gameObject.AddComponent<PulseEffect>();
            pulse.amount = 0.5f;
            pulse.speed = Mathf.PI / 2f;
            pulse.spacing = 0f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();
            float width = Width(text, 0);

            pulse.Play();
            fx.Tick(1f); // sin(π/2) = 1 → scale 1.5
            Assert.AreEqual(width * 1.5f, Width(text, 0), 0.01f);

            pulse.Stop();
            fx.Tick(0f);
            Assert.AreEqual(width, Width(text, 0), 0.01f);
        }

        [Test]
        public void Wobble_RotatesAroundCenter()
        {
            TextMeshProUGUI text = CreateText("W");
            var wobble = text.gameObject.AddComponent<WobbleEffect>();
            wobble.angle = 90f;
            wobble.speed = Mathf.PI / 2f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();
            Vector3 center = (Vertex(text, 0, 0) + Vertex(text, 0, 2)) * 0.5f;
            Vector3 corner = Vertex(text, 0, 0);

            wobble.Play();
            fx.Tick(1f); // 90°
            Vector3 rotated = Vertex(text, 0, 0);
            Vector3 newCenter = (Vertex(text, 0, 0) + Vertex(text, 0, 2)) * 0.5f;
            Assert.AreEqual(center.x, newCenter.x, 0.01f, "rotates around its own center");
            Assert.AreEqual(center.y, newCenter.y, 0.01f);
            Assert.AreEqual((corner - center).magnitude, (rotated - center).magnitude, 0.01f);
            Assert.Greater((rotated - corner).magnitude, 0.01f);
        }

        [Test]
        public void Rainbow_TintsCharacters_StaticWhenSpeedIsZero()
        {
            TextMeshProUGUI text = CreateText("RGB");
            text.color = Color.white;
            var rainbow = text.gameObject.AddComponent<RainbowEffect>();
            rainbow.speed = 0f;
            rainbow.spacing = 0f; // hue 0 = red
            rainbow.saturation = 1f;
            Assert.IsFalse(rainbow.IsAnimated);
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            rainbow.Play();
            fx.Tick(0f);
            Color32 c = VertexColor(text, 0, 0);
            Assert.AreEqual(255, c.r);
            Assert.Less(c.g, 5);
            Assert.Less(c.b, 5);
        }

        [Test]
        public void FadeIn_StartsHidden_EndsVisible_AndFinishes()
        {
            TextMeshProUGUI text = CreateText("Fade");
            var fade = text.gameObject.AddComponent<FadeInEffect>();
            fade.charDelay = 0.1f;
            fade.charDuration = 0.2f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            fade.Play();
            fx.Tick(0f);
            Assert.AreEqual(0, VertexColor(text, 0, 0).a);
            Assert.AreEqual(0, VertexColor(text, 3, 0).a);

            fx.Tick(0.25f);
            Assert.AreEqual(255, VertexColor(text, 0, 0).a, "first character done");
            Assert.Less(VertexColor(text, 3, 0).a, 255, "last character still fading");

            fx.Tick(1f);
            Assert.IsFalse(fade.IsPlaying);
            fx.Tick(0f);
            Assert.AreEqual(255, VertexColor(text, 3, 0).a);
        }

        [Test]
        public void FadeOut_HidesAndStaysHidden_OutOfUpdateLoop()
        {
            TextMeshProUGUI text = CreateText("Bye");
            var fade = text.gameObject.AddComponent<FadeOutEffect>();
            Assert.IsFalse(fade.PlayOnEnable, "must not hide the text as soon as it is enabled");
            Assert.IsFalse(fade.IsPlaying);
            fade.charDelay = 0.1f;
            fade.charDuration = 0.2f;
            int completed = 0;
            fade.onComplete.AddListener(() => completed++);
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            fade.Play();
            fx.Tick(0f);
            Assert.AreEqual(255, VertexColor(text, 2, 0).a);

            fx.Tick(0.25f);
            Assert.AreEqual(0, VertexColor(text, 0, 0).a, "first character gone");
            Assert.Greater(VertexColor(text, 2, 0).a, 0, "last character still fading");

            fx.Tick(1f);
            Assert.IsTrue(fade.IsHidden);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(0, VertexColor(text, 2, 0).a);
            Assert.IsFalse(fx.IsInUpdateLoop, "hidden text costs nothing per frame");
            Assert.IsTrue(fx.IsAwake);

            // A rebuild keeps it hidden; Stop shows it again.
            text.text = "Bye!";
            text.ForceMeshUpdate();
            Assert.AreEqual(0, VertexColor(text, 3, 0).a);
            Assert.AreEqual(1, completed);

            fade.Stop();
            fx.Tick(0f);
            Assert.AreEqual(255, VertexColor(text, 0, 0).a);
        }

        [Test]
        public void FadeOut_Backward_StartsAtTheEnd_AndHideNow()
        {
            TextMeshProUGUI text = CreateText("Bye");
            var fade = text.gameObject.AddComponent<FadeOutEffect>();
            fade.order = FadeOutEffect.Order.Backward;
            fade.charDelay = 0.1f;
            fade.charDuration = 0.2f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            fade.Play();
            fx.Tick(0f);
            fx.Tick(0.2f); // last character: 0..0.2 s, first: from 0.2 s
            Assert.AreEqual(0, VertexColor(text, 2, 0).a, "last character gone first");
            Assert.AreEqual(255, VertexColor(text, 0, 0).a);

            fade.Stop();
            fx.Tick(0f);
            fade.HideNow();
            fx.Tick(0f);
            Assert.AreEqual(0, VertexColor(text, 0, 0).a);
            Assert.IsTrue(fade.IsHidden);
        }

        [Test]
        public void FadeOut_DeactivatingOnComplete_IsSafe()
        {
            TextMeshProUGUI text = CreateText("Bye");
            var fade = text.gameObject.AddComponent<FadeOutEffect>();
            fade.charDuration = 0.1f;
            fade.onComplete.AddListener(() => text.gameObject.SetActive(false));
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            fade.Play();
            fx.Tick(0f);
            Assert.DoesNotThrow(() => fx.Tick(1f));
            Assert.IsFalse(text.gameObject.activeSelf);
            Assert.IsFalse(fx.IsInUpdateLoop);
        }

        [Test]
        public void TypewriterSound_ListensWithoutErrors()
        {
            TextMeshProUGUI text = CreateText("Hello world");
            var typer = text.gameObject.AddComponent<TypewriterEffect>();
            var sound = text.gameObject.AddComponent<TypewriterSound>();
            sound.clips = new[] { AudioClip.Create("tick", 441, 1, 44100, false) };
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            typer.Play();
            fx.Tick(0f);
            fx.Tick(0.1f);
            typer.Skip();
            Assert.DoesNotThrow(() => fx.Tick(0f));
            Assert.IsTrue(typer.IsComplete || typer.IsPlaying);
            Object.DestroyImmediate(sound.clips[0]);
        }

        [Test]
        public void Shake_WithDuration_CalmsDownAndStops()
        {
            TextMeshProUGUI text = CreateText("Shake");
            var shake = text.gameObject.AddComponent<ShakeEffect>();
            shake.amplitude = 10f;
            shake.duration = 1f;
            shake.falloff = 1f; // amplitude decreases linearly over the whole duration
            Assert.IsFalse(shake.IsLooping);
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();
            Vector3 original = Vertex(text, 0, 0);

            shake.Play();
            fx.Tick(0.9f); // 10% of the amplitude left
            Assert.LessOrEqual(Mathf.Abs(Vertex(text, 0, 0).x - original.x), 1.01f);
            Assert.IsTrue(shake.IsPlaying);

            fx.Tick(0.2f);
            Assert.IsFalse(shake.IsPlaying);
            fx.Tick(0f);
            Assert.AreEqual(original.x, Vertex(text, 0, 0).x, 0.001f);
            Assert.AreEqual(original.y, Vertex(text, 0, 0).y, 0.001f);
        }

        [Test]
        public void SkipSpaces_SpacesDoNotDelayTheNextCharacter()
        {
            TextMeshProUGUI text = CreateText("A B");
            var fade = text.gameObject.AddComponent<FadeInEffect>();
            fade.charDelay = 1f;
            fade.charDuration = 1f;
            var fx = text.GetComponent<TextFX>();
            text.ForceMeshUpdate();

            fade.Play();
            fx.Tick(1.5f); // "B" is the 2nd visible character: starts at 1 s, half faded
            Assert.AreEqual(128, VertexColor(text, 2, 0).a, 2);

            fade.skipSpaces = false;
            fade.Play();
            fx.Tick(1.5f); // counted as the 3rd character: starts at 2 s
            Assert.AreEqual(0, VertexColor(text, 2, 0).a);
        }

        [Test]
        public void TagArgs_ParseColors()
        {
            Assert.AreEqual(Color.red, TagArgs.Parse("=red").GetColor("c", "", Color.white));
            Assert.AreEqual(Color.green, TagArgs.Parse("c=#00ff00").GetColor("c", "", Color.white));
            Assert.AreEqual(Color.white, TagArgs.Parse("c=nope").GetColor("c", "", Color.white), "fallback");
        }

        [Test]
        public void ColorFlash_TagColor_OverridesComponentColor()
        {
            TextMeshProUGUI text = CreateText("");
            text.color = Color.white;
            var flash = text.gameObject.AddComponent<ColorFlashEffect>();
            SetTag(flash, "flash");
            flash.color = Color.black;
            flash.rate = 1f;
            flash.duration = 1f;
            text.text = "<flash>A</flash><flash=#0000ff>B</flash>";
            text.ForceMeshUpdate(false, true);
            var fx = text.GetComponent<TextFX>();

            flash.Play();
            fx.Tick(0.5f); // peak
            Color32 a = VertexColor(text, 0, 0);
            Color32 b = VertexColor(text, 1, 0);
            Assert.Less(a.b, 10, "component color: black");
            Assert.Less(b.r, 10);
            Assert.AreEqual(255, b.b, 1, "tag color: blue");
        }

        [Test]
        public void BounceIn_TagDelay_ChangesTiming()
        {
            TextMeshProUGUI text = CreateText("");
            var bounce = text.gameObject.AddComponent<BounceInEffect>();
            SetTag(bounce, "bounce");
            bounce.charDelay = 0f;
            bounce.charDuration = 1f;
            text.text = "<bounce d=10>AB</bounce>";
            text.ForceMeshUpdate(false, true);
            var fx = text.GetComponent<TextFX>();

            bounce.Play();
            fx.Tick(2f);
            Assert.AreEqual(255, VertexColor(text, 0, 0).a, "A done");
            Assert.AreEqual(0, VertexColor(text, 1, 0).a, "B waits 10 s");
            Assert.IsTrue(bounce.IsPlaying, "the end time uses the tag delay");
        }

        static float WaveOffset(TMP_Text text)
        {
            var wave = text.gameObject.AddComponent<WaveEffect>();
            wave.amplitude = 10f;
            wave.speed = Mathf.PI / 2f;
            text.ForceMeshUpdate();
            float original = Vertex(text, 0, 0).y;
            wave.Play();
            text.GetComponent<TextFX>().Tick(1f); // sin(π/2) = 1 → full amplitude
            return Vertex(text, 0, 0).y - original;
        }

        [Test]
        public void UnitScale_WorldTextUsesWorldMultiplier()
        {
            var settings = ScriptableObject.CreateInstance<TextFXSettings>();
            settings.uiUnitScale = 1f;
            settings.worldUnitScale = 0.1f;
            TextFXSettings.Set(settings);
            try
            {
                var world = new GameObject("WorldText");
                world.transform.SetParent(_canvas.transform, false);
                var tmp = world.AddComponent<TextMeshPro>();
                tmp.text = "World";
                Assert.IsTrue(TextFXSettings.IsWorld(tmp));
                Assert.AreEqual(1f, WaveOffset(tmp), 0.01f, "10 × 0.1");

                Assert.AreEqual(10f, WaveOffset(CreateText("UI")), 0.01f, "10 × 1");
            }
            finally
            {
                TextFXSettings.Set(null);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TimeScale_ChosenPerTextKind()
        {
            var settings = ScriptableObject.CreateInstance<TextFXSettings>();
            settings.uiUseTimeScale = false;
            settings.worldUseTimeScale = true;
            var go = new GameObject("WorldText");
            go.transform.SetParent(_canvas.transform, false);
            var world = go.AddComponent<TextMeshPro>();

            Assert.IsTrue(settings.UsesTimeScale(world));
            Assert.IsFalse(settings.UsesTimeScale(CreateText("UI")));
            Object.DestroyImmediate(settings);
        }
    }
}
