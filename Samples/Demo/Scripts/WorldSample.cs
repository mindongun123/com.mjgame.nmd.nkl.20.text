using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl.sample
{
    /// <summary>
    /// World (3D) texts: TextMeshPro components on objects, not UI.
    /// - Nameplate: TextExtrude + Wave on a world text (distances use the World scale of TextFXSettings).
    /// - Spinning sign: ArcLayout + Rainbow, turning with the object.
    /// - Damage numbers: pooled world texts with BounceIn, then FadeOut; a crit uses a built-in tag in
    ///   SetNumber's prefix. Pool items are reset with TextFX.ResetAll before reuse.
    /// - Orbiter: a looping Wave on an object that leaves the camera; it is skipped while off screen
    ///   (TextFX.CullHiddenTexts).
    /// - Pause: Time.timeScale = 0 stops world texts (World uses timeScale) while UI texts keep moving.
    /// </summary>
    public sealed class WorldSample : MonoBehaviour
    {
        [SerializeField] Transform _spinner;
        [SerializeField] float _spinSpeed = 45f;
        [SerializeField] Transform _orbiter;
        [SerializeField] Vector3 _orbitCenter = new Vector3(0f, 3f, 2f);
        [SerializeField] float _orbitRadius = 14f;
        [SerializeField] Transform _target;
        [SerializeField] TextMeshPro _damageTemplate;
        [SerializeField] float _autoHitEvery = 0.9f;
        [SerializeField] TMP_Text _pauseLabel;

        sealed class DamageNumber
        {
            public TextMeshPro Text;
            public TextFX Fx;
            public BounceInEffect Bounce;
            public FadeOutEffect Fade;
            public float Age;
            public bool Fading;
        }

        readonly Stack<DamageNumber> _pool = new Stack<DamageNumber>();
        readonly List<DamageNumber> _active = new List<DamageNumber>();
        float _nextHit;
        float _orbitAngle;

        void Awake() => _damageTemplate.gameObject.SetActive(false);

        void OnDisable()
        {
            // Leaving the tab: never leave the game paused.
            Time.timeScale = 1f;
            if (_pauseLabel != null) _pauseLabel.text = "Pause";
        }

        void Update()
        {
            float dt = Time.deltaTime; // scaled: everything here stops with the pause, like the world texts
            if (_spinner != null) _spinner.Rotate(0f, _spinSpeed * dt, 0f, Space.World);

            if (_orbiter != null)
            {
                // A wide circle: about half of the time the object is outside the camera view.
                _orbitAngle += dt * 0.6f;
                _orbiter.position = _orbitCenter + new Vector3(Mathf.Cos(_orbitAngle) * _orbitRadius, 0f, Mathf.Sin(_orbitAngle) * 2f);
            }

            if (_autoHitEvery > 0f && Time.time >= _nextHit)
            {
                _nextHit = Time.time + _autoHitEvery;
                Hit(Random.value < 0.25f);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                DamageNumber n = _active[i];
                n.Age += dt;
                n.Text.transform.position += Vector3.up * (dt * 0.8f);
                if (!n.Fading && n.Age > 0.7f)
                {
                    n.Fading = true;
                    n.Fade.Play(); // onComplete returns it to the pool
                }
            }
        }

        public void HitNormal() => Hit(false);

        public void HitCrit() => Hit(true);

        void Hit(bool crit)
        {
            DamageNumber n = _pool.Count > 0 ? _pool.Pop() : Create();
            n.Age = 0f;
            n.Fading = false;
            n.Text.transform.position = _target.position + new Vector3(Random.Range(-0.6f, 0.6f), 1.2f, -0.5f);
            n.Text.gameObject.SetActive(true);

            // Pooled item: clear the previous effects and mesh before the new content.
            n.Fx.ResetAll();
            int damage = crit ? Random.Range(800, 2000) : Random.Range(40, 300);
            n.Text.color = crit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            n.Text.fontSize = crit ? 9f : 6f;
            // Zero-GC formatting; the built-in <shake> tag in the prefix works too (one string per call then).
            if (crit) n.Text.SetNumber(damage, "<shake=3>", "!</shake>");
            else n.Text.SetNumber(damage);
            n.Fx.PlayAll(); // bounce + the tag effects
            n.Fade.Stop();  // FadeOut only starts when the number has lived a bit
            _active.Add(n);
        }

        DamageNumber Create()
        {
            TextMeshPro text = Instantiate(_damageTemplate, transform);
            text.name = "Damage";
            var n = new DamageNumber
            {
                Text = text,
                Fx = text.GetComponent<TextFX>(),
                Bounce = text.GetComponent<BounceInEffect>(),
                Fade = text.GetComponent<FadeOutEffect>(),
            };
            n.Fade.onComplete.AddListener(() => Release(n));
            return n;
        }

        void Release(DamageNumber n)
        {
            _active.Remove(n);
            n.Text.gameObject.SetActive(false);
            _pool.Push(n);
        }

        public void TogglePause()
        {
            bool pause = Time.timeScale > 0f;
            Time.timeScale = pause ? 0f : 1f;
            if (_pauseLabel != null) _pauseLabel.text = pause ? "Resume" : "Pause";
        }
    }
}
