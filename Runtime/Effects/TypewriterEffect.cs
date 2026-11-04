using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Reveals characters one by one (dialogue, tutorials). Hides characters through vertex alpha,
    /// so the layout does not jump and the text is not regenerated while typing.
    /// <para>Tags inside the text (removed from what is shown):</para>
    /// <list type="bullet">
    /// <item>&lt;p=0.5&gt; or &lt;pause=0.5&gt;: wait 0.5 s before the next character.</item>
    /// <item>&lt;speed=10&gt;...&lt;/speed&gt;: 10 characters per second; &lt;speed=x2&gt; doubles the base speed.</item>
    /// <item>&lt;ev=shake&gt; or &lt;event=shake&gt;: raises <see cref="onEvent"/>("shake") when reached.</item>
    /// </list>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Typewriter")]
    public sealed class TypewriterEffect : TextEffect
    {
        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Colors32;
        // Reveals the whole text by default.
        public override string DefaultTag => "";

        // Its events may run game code: preview it with the Preview button only.
        protected override bool PreviewsInEditMode => false;

        public float charactersPerSecond = 30f;
        [Tooltip("Fade length of each revealed character, in seconds; 0 = pop in.")]
        public float fadeDuration = 0.08f;
        [Tooltip("Extra pause after punctuation, in seconds.")]
        public float punctuationPause = 0.25f;
        public string punctuation = ".,!?;:";
        [Tooltip("Start typing again from the beginning when the text content changes while playing.")]
        public bool restartOnTextChange = true;

        [Tooltip("Character index in the text. Use it for typing sounds.")]
        public UnityEvent<int> onCharacterRevealed = new UnityEvent<int>();
        public UnityEvent onComplete = new UnityEvent();
        [Tooltip("Raised by <ev=name> / <event=name> tags when typing reaches them.")]
        public UnityEvent<string> onEvent = new UnityEvent<string>();

        enum CommandKind
        {
            Pause,
            Speed,
            ResetSpeed,
            Event,
        }

        struct Command
        {
            public int Character;
            public CommandKind Kind;
            public float Value;
            public string Text;
        }

        readonly List<Command> _commands = new List<Command>();
        float[] _revealTimes = Array.Empty<float>();
        int _revealed;
        int _commandCursor;
        float _speed;
        float _nextReveal;
        bool _skip;
        string _lastText;

        public bool IsComplete { get; private set; }

        /// <summary>Sets a new text and types it from the start.</summary>
        public void Show(string text)
        {
            Fx.Text.text = text;
            Play();
        }

        /// <summary>Reveals everything immediately (e.g. when the player taps).</summary>
        public void Skip() => _skip = true;

        protected override void OnPlay() => ResetState();

        protected internal override bool ClaimsPointTag(string text, int start, int length) =>
            Is(text, start, length, "p") || Is(text, start, length, "pause") || Is(text, start, length, "speed") ||
            Is(text, start, length, "ev") || Is(text, start, length, "event");

        static bool Is(string text, int start, int length, string name) =>
            length == name.Length && string.Compare(text, start, name, 0, length, StringComparison.OrdinalIgnoreCase) == 0;

        protected override void OnTextRebuilt()
        {
            if (_revealTimes.Length < AffectedCount) _revealTimes = new float[Mathf.NextPowerOfTwo(AffectedCount)];
            BuildCommands();

            string current = Fx.Text.text;
            if (restartOnTextChange && _lastText != null && current != _lastText)
            {
                ResetTime();
                ResetState();
            }
            _lastText = current;
        }

        void BuildCommands()
        {
            _commands.Clear();
            TMP_TextInfo info = Fx.Text.textInfo;
            IReadOnlyList<TagSpan> spans = Fx.Spans;
            for (int i = 0; i < spans.Count; i++)
            {
                TagSpan span = spans[i];
                if (!span.IsPoint) continue;
                TextFX.MapToCharacters(info, span.Start, span.Start, out int character, out _);
                string value = TagArgs.Parse(span.Args).TryGet("", out string main) ? main : "";
                var command = new Command { Character = character, Text = value };

                switch (span.Name)
                {
                    case "p":
                    case "pause":
                        command.Kind = CommandKind.Pause;
                        command.Value = ParseFloat(value, 0f);
                        break;
                    case "speed":
                        // "x2" = multiplier of the base speed, "10" = characters per second.
                        bool multiplier = value.StartsWith("x", StringComparison.OrdinalIgnoreCase);
                        float number = ParseFloat(multiplier ? value.Substring(1) : value, 1f);
                        command.Kind = CommandKind.Speed;
                        command.Value = multiplier ? charactersPerSecond * number : number;
                        break;
                    case "/speed":
                        command.Kind = CommandKind.ResetSpeed;
                        break;
                    case "ev":
                    case "event":
                        command.Kind = CommandKind.Event;
                        break;
                    default:
                        continue;
                }
                _commands.Add(command);
            }
        }

        static float ParseFloat(string value, float fallback) =>
            float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : fallback;

        void ResetState()
        {
            _revealed = 0;
            _commandCursor = 0;
            _speed = charactersPerSecond;
            _nextReveal = 0f;
            _skip = false;
            IsComplete = false;
        }

        /// <summary>Runs every command placed before <paramref name="character"/> that has not run yet.</summary>
        void RunCommands(int character)
        {
            while (_commandCursor < _commands.Count && _commands[_commandCursor].Character <= character)
            {
                Command command = _commands[_commandCursor++];
                switch (command.Kind)
                {
                    case CommandKind.Pause:
                        if (!_skip) _nextReveal = Mathf.Max(_nextReveal, Time) + command.Value;
                        break;
                    case CommandKind.Speed:
                        _speed = command.Value;
                        break;
                    case CommandKind.ResetSpeed:
                        _speed = charactersPerSecond;
                        break;
                    case CommandKind.Event:
                        onEvent.Invoke(command.Text);
                        break;
                }
            }
        }

        protected override bool Apply(TMP_TextInfo info)
        {
            while (_revealed < AffectedCount)
            {
                int c = CharacterIndexAt(_revealed);
                RunCommands(c);
                if (!_skip && Time < _nextReveal) break;

                _revealTimes[_revealed] = _skip ? float.NegativeInfinity : _nextReveal;
                _revealed++;
                _nextReveal += _speed > 0f ? 1f / _speed : 0f;
                if (IsPunctuation(info.characterInfo[c].character)) _nextReveal += punctuationPause;
                onCharacterRevealed.Invoke(c);
            }
            if (_revealed >= AffectedCount) RunCommands(int.MaxValue); // tags after the last character
            _skip = false;

            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float alpha = n >= _revealed ? 0f
                        : fadeDuration > 0f ? Mathf.Clamp01((Time - _revealTimes[n]) / fadeDuration) : 1f;
                    if (alpha < 1f) MultiplyAlpha(info, c, alpha);
                }
            }

            if (_revealed < AffectedCount) return true;
            float lastReveal = AffectedCount > 0 ? _revealTimes[AffectedCount - 1] : 0f;
            if (Time < lastReveal + fadeDuration) return true;

            IsComplete = true;
            onComplete.Invoke();
            return false;
        }

        bool IsPunctuation(char ch) => !string.IsNullOrEmpty(punctuation) && punctuation.IndexOf(ch) >= 0;
    }
}
