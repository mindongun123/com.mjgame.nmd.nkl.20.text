using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl.sample
{
    /// <summary>
    /// Everything not shown on the other tabs:
    /// - Built-in tags without components: &lt;pulse&gt;, &lt;flash&gt;, &lt;shine&gt;, &lt;bounce&gt;, &lt;fade&gt;, ShineEffect.
    /// - Dialogue: TypewriterEffect with &lt;ev=...&gt; events, TypewriterSound, and FadeOutEffect to close.
    /// - Formatting: every TMPTextExtensions setter, re-rendered only when the shown value changes.
    /// - Runtime settings: reduced motion (TextFXSettings.SetPlayerMotionScale) and the update rate.
    /// </summary>
    public sealed class ExtrasSample : MonoBehaviour
    {
        [Header("Built-in tags")]
        [SerializeField] TextFX[] _replayable;

        [Header("Dialogue")]
        [SerializeField] TypewriterEffect _dialogue;
        [SerializeField] FadeOutEffect _dialogueOut;
        [SerializeField] TypewriterSound _sound;
        [SerializeField] ShakeEffect _portraitShake;
        [SerializeField] BounceInEffect _portraitBounce;
        [TextArea]
        [SerializeField] string[] _lines =
        {
            "Hello, traveler!<p=0.4> Welcome to the <shine>village</shine>.",
            "Watch out<p=0.3>.<p=0.3>.<p=0.3>. <ev=shake><shake>a dragon!</shake>",
            "<speed=x0.4>Take your time...</speed> <ev=happy>or <speed=x3>hurry up, the shop closes soon!</speed>",
        };

        [Header("Formatting")]
        [SerializeField] TMP_Text _number;
        [SerializeField] TMP_Text _abbreviated;
        [SerializeField] TMP_Text _idle;
        [SerializeField] TMP_Text _decimal;
        [SerializeField] TMP_Text _percent;
        [SerializeField] TMP_Text _fraction;
        [SerializeField] TMP_Text _clock;
        [SerializeField] TMP_Text _compact;

        [Header("Settings")]
        [SerializeField] TMP_Text _motionLabel;
        [SerializeField] TMP_Text _rateLabel;

        static readonly TimeLabels Vietnamese = new TimeLabels(" ngày", " giờ", " phút", " giây");

        TextFXSettings _settings;
        int _line = -1;
        bool _reducedMotion;
        float _time;
        long _shownNumber = -1;
        int _shownSecond = -1;

        void Awake()
        {
            // A runtime copy, so the buttons below never modify the settings asset of the project.
            _settings = Instantiate(TextFXSettings.Instance);
            _settings.qualityOverrides = new TextFXSettings.QualityOverride[0];
            TextFXSettings.Set(_settings);

            if (_sound != null && (_sound.clips == null || _sound.clips.Length == 0))
                _sound.clips = new[] { MakeClick("click-a", 1700f), MakeClick("click-b", 2100f), MakeClick("click-c", 1400f) };
            _dialogue.onEvent.AddListener(OnDialogueEvent);
            UpdateLabels();
        }

        void Start() => NextLine();

        void OnDestroy()
        {
            TextFXSettings.Set(null);
            TextFXSettings.SetPlayerMotionScale(-1f);
        }

        // ---------- Built-in tags ----------

        public void ReplayTags()
        {
            foreach (TextFX fx in _replayable)
                if (fx != null) fx.PlayAll();
        }

        // ---------- Dialogue ----------

        public void NextLine()
        {
            _line = (_line + 1) % _lines.Length;
            _dialogueOut.Stop(); // visible again if it was closed
            _dialogue.Show(_lines[_line]);
        }

        public void SkipLine() => _dialogue.Skip();

        public void CloseDialogue() => _dialogueOut.Play();

        void OnDialogueEvent(string name)
        {
            if (name == "shake" && _portraitShake != null) _portraitShake.Play();
            if (name == "happy" && _portraitBounce != null) _portraitBounce.Play();
        }

        /// <summary>Short sine "blip" so the sample needs no audio file. Use your own clips in a game.</summary>
        static AudioClip MakeClick(string name, float frequency)
        {
            const int rate = 44100;
            int length = rate * 35 / 1000;
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 110f) * 0.35f;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------- Formatting ----------

        void Update()
        {
            _time += Time.unscaledDeltaTime;

            // Values change every frame here, but text is only set when what is shown changes.
            long number = (long)(_time * 98765.4321);
            if (number != _shownNumber)
            {
                _shownNumber = number;
                _number.SetNumber(number, "Gold: ");
                _abbreviated.SetAbbreviated(number * 37, 1, 1000, "Power ");
                _idle.SetAbbreviated(System.Math.Pow(10.0, 3.0 + (_time * 2.0) % 60.0), 2, 1000, "Idle: ");
                _decimal.SetDecimal(number % 10000 / 100.0, 2, "x", " speed");
                _percent.SetPercent(_time * 0.1 % 1.0, 0, "Loading ");
                _fraction.SetFraction((long)(_time * 2) % 11, 10, "Quest ");
            }

            int second = Mathf.CeilToInt(Mathf.Max(0f, 93784f - _time));
            if (second == _shownSecond) return;
            _shownSecond = second;
            _clock.SetTime(second, TimeFormat.Auto, true, "Event ends ");
            _compact.SetTime(second, TimeFormat.Compact, true, "Còn ", null, Vietnamese);
        }

        // ---------- Settings ----------

        public void ToggleReducedMotion()
        {
            _reducedMotion = !_reducedMotion;
            // Player option: wins over the asset and quality levels. 0 = colors and fades only.
            TextFXSettings.SetPlayerMotionScale(_reducedMotion ? 0f : -1f);
            UpdateLabels();
        }

        public void ToggleUpdateRate()
        {
            _settings.maxUpdatesPerSecond = _settings.maxUpdatesPerSecond == 0 ? 15 : 0;
            UpdateLabels();
        }

        void UpdateLabels()
        {
            if (_motionLabel != null) _motionLabel.text = _reducedMotion ? "Motion: reduced" : "Motion: full";
            if (_rateLabel != null)
                _rateLabel.text = _settings.maxUpdatesPerSecond == 0 ? "Rate: every frame" : "Rate: " + _settings.maxUpdatesPerSecond + "/s";
        }
    }
}
