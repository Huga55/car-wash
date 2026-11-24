using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace LittleGarage
{
    /// <summary>
    /// Procedural audio runtime. All clips are generated in memory (22.05 kHz mono).
    /// Supports looped tool sounds, one-shot SFX, music tracks, buses and PlayerPrefs persistence.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int SampleRate = 22050;
        private const int OneShotPoolSize = 8;
        private const float DuckVolume = 0.35f;
        private const float FadeSpeed = 3.5f;

        private static readonly string[] BusNames = { "Music", "Washing", "UI", "Characters", "Ambient" };

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoopChannel> _loops = new Dictionary<string, LoopChannel>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _busVolume = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _busEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioMixerGroup> _mixerGroups = new Dictionary<string, AudioMixerGroup>(StringComparer.Ordinal);

        private AudioSource[] _oneShots;
        private int _oneShotIndex;
        private AudioSource _musicSource;
        private string _musicTrack;
        private float _musicBaseVolume = 0.7f;
        private float _musicDuck = 1f;
        private float _musicDuckTarget = 1f;
        private AudioMixer _mixer;

        private sealed class LoopChannel
        {
            public AudioSource Source;
            public float TargetIntensity;
            public float CurrentIntensity;
            public string Bus;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadPrefs();
            TryLoadMixer();
            EnsureOneShotPool();
            EnsureMusicSource();
            BuildClips();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (KeyValuePair<string, AudioClip> kv in _clips)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _clips.Clear();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateMusicDuck(dt);
            UpdateLoops(dt);
        }

        public void Play(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            AudioClip clip = GetClip(key);
            if (clip == null) return;

            string bus = BusForKey(key);
            if (!IsBusAudible(bus)) return;

            EnsureOneShotPool();
            AudioSource src = _oneShots[_oneShotIndex];
            _oneShotIndex = (_oneShotIndex + 1) % _oneShots.Length;
            ConfigureSource(src, bus);
            src.pitch = 1f;
            src.PlayOneShot(clip, BusGain(bus));
        }

        public void SetLoop(string key, bool on, float intensity)
        {
            if (string.IsNullOrEmpty(key)) return;
            LoopChannel ch;
            if (!_loops.TryGetValue(key, out ch))
            {
                if (!on) return;
                AudioClip clip = GetClip(key);
                if (clip == null) return;
                string bus = BusForKey(key);
                var go = new GameObject("Loop_" + key);
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.clip = clip;
                src.spatialBlend = 0f;
                src.volume = 0f;
                ConfigureSource(src, bus);
                src.Play();
                ch = new LoopChannel { Source = src, Bus = bus, TargetIntensity = 0f, CurrentIntensity = 0f };
                _loops[key] = ch;
            }

            ch.TargetIntensity = on ? Mathf.Clamp01(intensity) : 0f;
            if (!on && ch.CurrentIntensity <= 0.001f && ch.TargetIntensity <= 0f)
            {
                if (ch.Source != null) ch.Source.Stop();
            }
            else if (on && ch.Source != null && !ch.Source.isPlaying)
            {
                ConfigureSource(ch.Source, ch.Bus);
                ch.Source.Play();
            }
        }

        public void PlayMusic(string track)
        {
            if (string.IsNullOrEmpty(track)) track = "menu";
            if (string.Equals(_musicTrack, track, StringComparison.Ordinal) && _musicSource != null && _musicSource.isPlaying)
                return;

            EnsureMusicSource();
            AudioClip clip = GetClip("music_" + track);
            if (clip == null) clip = GetClip("music_menu");
            _musicTrack = track;
            _musicSource.clip = clip;
            _musicSource.loop = true;
            ConfigureSource(_musicSource, "Music");
            _musicSource.volume = 0f;
            _musicSource.Play();
        }

        public void SetVolume(string bus, float v)
        {
            string b = NormalizeBus(bus);
            float clamped = Mathf.Clamp01(v);
            _busVolume[b] = clamped;
            PlayerPrefs.SetFloat(VolumeKey(b), clamped);
            PlayerPrefs.Save();
            ApplyBusVolume(b);
        }

        public float GetVolume(string bus)
        {
            string b = NormalizeBus(bus);
            float v;
            return _busVolume.TryGetValue(b, out v) ? v : 0.7f;
        }

        public void SetBusEnabled(string bus, bool enabled)
        {
            string b = NormalizeBus(bus);
            _busEnabled[b] = enabled;
            PlayerPrefs.SetInt(EnabledKey(b), enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyBusVolume(b);
            if (string.Equals(b, "Music", StringComparison.Ordinal) && _musicSource != null)
            {
                if (!enabled) _musicSource.Pause();
                else if (_musicSource.clip != null && !_musicSource.isPlaying) _musicSource.Play();
            }
        }

        public bool GetBusEnabled(string bus)
        {
            string b = NormalizeBus(bus);
            bool e;
            return _busEnabled.TryGetValue(b, out e) ? e : true;
        }

        private void LoadPrefs()
        {
            for (int i = 0; i < BusNames.Length; i++)
            {
                string b = BusNames[i];
                _busVolume[b] = PlayerPrefs.GetFloat(VolumeKey(b), DefaultVolume(b));
                _busEnabled[b] = PlayerPrefs.GetInt(EnabledKey(b), 1) == 1;
            }
        }

        private static float DefaultVolume(string bus)
        {
            if (string.Equals(bus, "Music", StringComparison.Ordinal)) return 0.55f;
            if (string.Equals(bus, "Ambient", StringComparison.Ordinal)) return 0.45f;
            return 0.75f;
        }

        private static string NormalizeBus(string bus)
        {
            if (string.IsNullOrEmpty(bus)) return "UI";
            for (int i = 0; i < BusNames.Length; i++)
            {
                if (string.Equals(BusNames[i], bus, StringComparison.OrdinalIgnoreCase))
                    return BusNames[i];
            }
            return "UI";
        }

        private static string VolumeKey(string bus) { return "LittleGarage.Vol." + bus; }
        private static string EnabledKey(string bus) { return "LittleGarage.On." + bus; }

        private void TryLoadMixer()
        {
            _mixer = Resources.Load<AudioMixer>("Audio/AudioMixer");
            _mixerGroups.Clear();
            if (_mixer == null) return;
            for (int i = 0; i < BusNames.Length; i++)
            {
                AudioMixerGroup[] groups = _mixer.FindMatchingGroups(BusNames[i]);
                if (groups != null && groups.Length > 0)
                    _mixerGroups[BusNames[i]] = groups[0];
            }
        }

        private void EnsureOneShotPool()
        {
            if (_oneShots != null && _oneShots.Length == OneShotPoolSize) return;
            _oneShots = new AudioSource[OneShotPoolSize];
            for (int i = 0; i < _oneShots.Length; i++)
            {
                var go = new GameObject("OneShot_" + i);
                go.transform.SetParent(transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                _oneShots[i] = src;
            }
        }

        private void EnsureMusicSource()
        {
            if (_musicSource != null) return;
            var go = new GameObject("Music");
            go.transform.SetParent(transform, false);
            _musicSource = go.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
            ConfigureSource(_musicSource, "Music");
        }

        private void ConfigureSource(AudioSource src, string bus)
        {
            if (src == null) return;
            string b = NormalizeBus(bus);
            AudioMixerGroup g;
            if (_mixer != null && _mixerGroups.TryGetValue(b, out g))
                src.outputAudioMixerGroup = g;
            else
                src.outputAudioMixerGroup = null;
        }

        private float BusGain(string bus)
        {
            string b = NormalizeBus(bus);
            if (!GetBusEnabled(b)) return 0f;
            return GetVolume(b);
        }

        private bool IsBusAudible(string bus)
        {
            return BusGain(bus) > 0.001f;
        }

        private void ApplyBusVolume(string bus)
        {
            string b = NormalizeBus(bus);
            if (string.Equals(b, "Music", StringComparison.Ordinal) && _musicSource != null)
                ConfigureSource(_musicSource, "Music");

            foreach (KeyValuePair<string, LoopChannel> kv in _loops)
            {
                if (string.Equals(kv.Value.Bus, b, StringComparison.Ordinal))
                    ConfigureSource(kv.Value.Source, b);
            }
        }

        private void UpdateMusicDuck(float dt)
        {
            bool washingActive = false;
            foreach (KeyValuePair<string, LoopChannel> kv in _loops)
            {
                if (!string.Equals(kv.Value.Bus, "Washing", StringComparison.Ordinal)) continue;
                if (kv.Value.CurrentIntensity > 0.02f || kv.Value.TargetIntensity > 0.02f)
                {
                    washingActive = true;
                    break;
                }
            }
            _musicDuckTarget = washingActive ? DuckVolume : 1f;
            _musicDuck = Mathf.MoveTowards(_musicDuck, _musicDuckTarget, dt * 2.5f);

            if (_musicSource == null) return;
            float target = _musicBaseVolume * BusGain("Music") * _musicDuck;
            _musicSource.volume = Mathf.MoveTowards(_musicSource.volume, target, dt * FadeSpeed);
        }

        private void UpdateLoops(float dt)
        {
            float fade = dt * FadeSpeed;
            foreach (KeyValuePair<string, LoopChannel> kv in _loops)
            {
                LoopChannel ch = kv.Value;
                if (ch.Source == null) continue;
                ch.CurrentIntensity = Mathf.MoveTowards(ch.CurrentIntensity, ch.TargetIntensity, fade);
                float vol = ch.CurrentIntensity * BusGain(ch.Bus);
                ch.Source.volume = vol;
                if (ch.TargetIntensity <= 0f && ch.CurrentIntensity <= 0.001f && ch.Source.isPlaying)
                    ch.Source.Stop();
            }
        }

        private string BusForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "UI";
            if (key.StartsWith("music_", StringComparison.Ordinal)) return "Music";
            if (key == "ui_click" || key == "ui" || key == "click") return "UI";
            if (key == "complete" || key == "complete_task" || key == "complete_job") return "UI";
            if (key == "engine") return "Characters";
            if (key == "water" || key == "foam" || key == "sponge" || key == "rinse" || key == "air" || key == "polish") return "Washing";
            if (key == "unscrew" || key == "wheel_install" || key == "part_install") return "UI";
            return "UI";
        }

        private AudioClip GetClip(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            AudioClip clip;
            if (_clips.TryGetValue(key, out clip) && clip != null) return clip;
            clip = BuildClip(key);
            if (clip != null) _clips[key] = clip;
            return clip;
        }

        private void BuildClips()
        {
            // Prebuild common clips to avoid first-use hitch.
            string[] keys = {
                "music_menu", "music_wash", "music_finale",
                "water", "foam", "sponge", "rinse", "air", "polish",
                "unscrew", "wheel_install", "part_install", "complete", "complete_task", "complete_job",
                "ui_click", "click", "engine"
            };
            for (int i = 0; i < keys.Length; i++) GetClip(keys[i]);
        }

        private AudioClip BuildClip(string key)
        {
            switch (key)
            {
                case "music_menu": return MakeMusic("music_menu", 8f, 0.28f, 220f, 330f, 0.015f);
                case "music_wash": return MakeMusic("music_wash", 8f, 0.24f, 196f, 294f, 0.02f);
                case "music_finale": return MakeMusic("music_finale", 6f, 0.3f, 262f, 392f, 0.01f);
                case "water": return MakeNoiseLoop("water", 1.6f, 0.35f, 0.55f, 0.0f);
                case "foam": return MakeNoiseLoop("foam", 1.8f, 0.22f, 0.25f, 0.08f);
                case "sponge": return MakeNoiseLoop("sponge", 1.2f, 0.18f, 0.18f, 0.15f);
                case "rinse": return MakeNoiseLoop("rinse", 1.5f, 0.32f, 0.65f, 0.0f);
                case "air": return MakeNoiseLoop("air", 1.7f, 0.28f, 0.75f, 0.0f);
                case "polish": return MakeToneLoop("polish", 1.4f, 110f, 0.12f, 0.04f);
                case "unscrew": return MakeUnscrew();
                case "wheel_install": return MakeThump("wheel_install", 0.28f, 90f, 0.55f);
                case "part_install": return MakeThump("part_install", 0.22f, 140f, 0.45f);
                case "complete":
                case "complete_task":
                case "complete_job": return MakeChime(key);
                case "ui_click":
                case "click": return MakeClick();
                case "engine": return MakeEngine();
                default:
                    if (key.StartsWith("music_", StringComparison.Ordinal)) return MakeMusic(key, 6f, 0.25f, 220f, 330f, 0.015f);
                    return MakeClick();
            }
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            int len = samples != null ? samples.Length : 1;
            if (len < 1) len = 1;
            AudioClip clip = AudioClip.Create(name, len, 1, SampleRate, false);
            clip.SetData(samples ?? new float[1], 0);
            return clip;
        }

        private static AudioClip MakeNoiseLoop(string name, float seconds, float amp, float brightness, float wobble)
        {
            int len = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            float[] data = new float[len];
            int seed = name.GetHashCode();
            System.Random rng = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float n = ((float)rng.NextDouble() * 2f - 1f);
                lp += (n - lp) * Mathf.Clamp01(brightness);
                float env = 0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * (1f + wobble * 6f));
                data[i] = lp * amp * env;
            }
            // Seamless loop fade.
            int edge = Mathf.Min(len / 20, 512);
            for (int i = 0; i < edge; i++)
            {
                float k = (float)i / edge;
                data[i] = Mathf.Lerp(data[len - edge + i], data[i], k);
            }
            return MakeClip(name, data);
        }

        private static AudioClip MakeToneLoop(string name, float seconds, float freq, float amp, float noise)
        {
            int len = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            float[] data = new float[len];
            System.Random rng = new System.Random(name.GetHashCode());
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float n = ((float)rng.NextDouble() * 2f - 1f);
                lp += (n - lp) * 0.15f;
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
                data[i] = (s * amp) + (lp * noise);
            }
            int edge = Mathf.Min(len / 20, 512);
            for (int i = 0; i < edge; i++)
            {
                float k = (float)i / edge;
                data[i] = Mathf.Lerp(data[len - edge + i], data[i], k);
            }
            return MakeClip(name, data);
        }

        private static AudioClip MakeMusic(string name, float seconds, float amp, float baseFreq, float altFreq, float shimmer)
        {
            int len = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            float[] data = new float[len];
            int[] scale = { 0, 3, 5, 7, 10, 12 };
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                int note = scale[(int)(t * 0.5f) % scale.Length];
                float freq = baseFreq * Mathf.Pow(2f, note / 12f);
                float freq2 = altFreq * Mathf.Pow(2f, scale[((int)(t * 0.25f) + 2) % scale.Length] / 12f);
                float melody = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                float pad = Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.25f;
                float bass = Mathf.Sin(2f * Mathf.PI * (baseFreq * 0.5f) * t) * 0.2f;
                float env = 0.8f + 0.2f * Mathf.Sin(t * Mathf.PI * 2f / seconds);
                data[i] = (melody + pad + bass) * amp * env + Mathf.Sin(t * 40f) * shimmer * amp;
            }
            int edge = Mathf.Min(len / 16, 1024);
            for (int i = 0; i < edge; i++)
            {
                float k = (float)i / edge;
                data[i] = Mathf.Lerp(data[len - edge + i], data[i], k);
            }
            return MakeClip(name, data);
        }

        private static AudioClip MakeClick()
        {
            int len = Mathf.RoundToInt(0.08f * SampleRate);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.25f) * env * 0.35f;
            }
            return MakeClip("ui_click", data);
        }

        private static AudioClip MakeChime(string name)
        {
            int len = Mathf.RoundToInt(0.9f * SampleRate);
            float[] data = new float[len];
            float[] notes = name.Contains("job") ? new[] { 523.25f, 659.25f, 783.99f, 1046.5f } : new[] { 659.25f, 783.99f, 987.77f };
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float s = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.08f;
                    float lt = t - start;
                    if (lt < 0f) continue;
                    float env = Mathf.Exp(-lt * 4.5f);
                    s += Mathf.Sin(2f * Mathf.PI * notes[n] * lt) * env * 0.22f;
                }
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip MakeUnscrew()
        {
            int len = Mathf.RoundToInt(0.35f * SampleRate);
            float[] data = new float[len];
            System.Random rng = new System.Random(91);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float click = Mathf.Exp(-((t % 0.07f) * 140f));
                float n = ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
                data[i] = (click * 0.35f + n) * Mathf.Clamp01(1f - t / 0.35f);
            }
            return MakeClip("unscrew", data);
        }

        private static AudioClip MakeThump(string name, float seconds, float freq, float amp)
        {
            int len = Mathf.RoundToInt(seconds * SampleRate);
            float[] data = new float[len];
            System.Random rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 18f);
                float n = ((float)rng.NextDouble() * 2f - 1f) * 0.1f * env;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * env * amp) + n;
            }
            return MakeClip(name, data);
        }

        private static AudioClip MakeEngine()
        {
            int len = Mathf.RoundToInt(0.7f * SampleRate);
            float[] data = new float[len];
            System.Random rng = new System.Random(77);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float n = ((float)rng.NextDouble() * 2f - 1f);
                lp += (n - lp) * 0.2f;
                float rev = 80f + t * 120f;
                float s = Mathf.Sin(2f * Mathf.PI * rev * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * rev * 2f * t) * 0.2f;
                float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01(1f - (t - 0.45f) * 3f);
                data[i] = (s + lp * 0.15f) * 0.35f * env;
            }
            return MakeClip("engine", data);
        }
    }
}
