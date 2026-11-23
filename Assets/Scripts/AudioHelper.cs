using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// All sound is generated in memory at 22.05 kHz mono: no audio files, no
/// download, no codec work on Android. Five permanent sources are routed to
/// five mixer groups (Music, Washing, UI, Characters, Ambient). Looping
/// sources are started once per stage and only their volume is automated, so
/// fast fingers never allocate or stack identical water sounds.
/// </summary>
public static class AudioManager
{
    private enum Bus { Music, Washing, Ui, Characters, Ambient }

    private static AudioSource _music;
    private static AudioSource _wash;
    private static AudioSource _ui;
    private static AudioSource _animal;
    private static AudioSource _ambient;
    private static AudioMixer _mixer;

    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, AudioClip> Loops = new Dictionary<string, AudioClip>();

    private static bool _muted;
    private static bool _musicEnabled = true;
    private static bool _sfxEnabled = true;
    private static float _masterVolume = 0.82f;
    private static float _musicVolume = 0.30f;
    private static float _sfxVolume = 0.75f;

    private static string _musicMode = "";
    private static string _toolId = "";
    private static bool _toolActive;
    private static bool _toolStopping;
    private static float _toolTarget;
    private static float _toolLevel;
    private static float _musicLevel;
    private static int _variation;

    public const int SampleRate = 22050;

    // Requested mix: music is a background bed (25-35%), washing and animal
    // voices stay clearly in front (65-80%), UI/ambient sit between (35-55%).
    private const float MusicBus = 0.30f;
    private const float WashingBus = 0.75f;
    private const float UiBus = 0.45f;
    private const float CharactersBus = 0.75f;
    private const float AmbientBus = 0.45f;

    private static float BusGain(Bus bus)
    {
        switch (bus)
        {
            case Bus.Music: return MusicBus;
            case Bus.Washing: return WashingBus;
            case Bus.Characters: return CharactersBus;
            case Bus.Ambient: return AmbientBus;
            default: return UiBus;
        }
    }

    /// <summary>Legacy "all sound" switch. The UI now also exposes separate
    /// music and effects switches; this one is the one-tap mute.</summary>
    public static bool Enabled
    {
        get { return !_muted; }
        set { Muted = !value; }
    }

    public static bool Muted
    {
        get { return _muted; }
        set
        {
            _muted = value;
            PlayerPrefs.SetInt(GameData.MuteKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool MusicEnabled
    {
        get { return _musicEnabled; }
        set
        {
            _musicEnabled = value;
            PlayerPrefs.SetInt(GameData.MusicKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool SfxEnabled
    {
        get { return _sfxEnabled; }
        set
        {
            _sfxEnabled = value;
            PlayerPrefs.SetInt(GameData.SfxKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float MasterVolume
    {
        get { return _masterVolume; }
        set { _masterVolume = Volume(value, GameData.MasterVolumeKey, _masterVolume); }
    }

    public static float MusicVolume
    {
        get { return _musicVolume; }
        set { _musicVolume = Volume(value, GameData.MusicVolumeKey, _musicVolume); }
    }

    public static float SfxVolume
    {
        get { return _sfxVolume; }
        set { _sfxVolume = Volume(value, GameData.SfxVolumeKey, _sfxVolume); }
    }

    private static float Volume(float value, string key, float fallback)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        return value;
    }

    public static void Init(GameObject host, AudioMixer mixer = null)
    {
        if (host == null) return;
        _muted = PlayerPrefs.GetInt(GameData.MuteKey, PlayerPrefs.GetInt(GameData.SoundKey, 1) == 0 ? 1 : 0) == 1;
        _musicEnabled = PlayerPrefs.GetInt(GameData.MusicKey, 1) == 1;
        _sfxEnabled = PlayerPrefs.GetInt(GameData.SfxKey, 1) == 1;
        _masterVolume = PlayerPrefs.GetFloat(GameData.MasterVolumeKey, _masterVolume);
        _musicVolume = PlayerPrefs.GetFloat(GameData.MusicVolumeKey, _musicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(GameData.SfxVolumeKey, _sfxVolume);
        _mixer = mixer;

        _musicMode = "menu";
        _music = Source(host, "Music", Bus.Music, true);
        _wash = Source(host, "Washing", Bus.Washing, true);
        _ui = Source(host, "UI", Bus.Ui, false);
        _animal = Source(host, "Characters", Bus.Characters, false);
        _ambient = Source(host, "Ambient", Bus.Ambient, false);

        _music.clip = Music("menu");
        _music.loop = true;
        _music.Play();
        _musicLevel = TargetMusic();
        _music.volume = _musicLevel;
    }

    private static AudioSource Source(GameObject host, string name, Bus bus, bool loop)
    {
        Transform found = host.transform.Find("Audio_" + name);
        AudioSource source;
        if (found != null)
        {
            source = found.GetComponent<AudioSource>();
        }
        else
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(host.transform, false);
            source = go.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.priority = bus == Bus.Music ? 220 : (bus == Bus.Washing ? 80 : 128);
        if (_mixer != null)
        {
            AudioMixerGroup[] groups = _mixer.FindMatchingGroups(Group(bus));
            if (groups != null && groups.Length > 0) source.outputAudioMixerGroup = groups[0];
        }
        return source;
    }

    private static string Group(Bus bus)
    {
        switch (bus)
        {
            case Bus.Music: return "Music";
            case Bus.Washing: return "Washing";
            case Bus.Characters: return "Characters";
            case Bus.Ambient: return "Ambient";
            default: return "UI";
        }
    }

    private static AudioSource BusSource(Bus bus)
    {
        switch (bus)
        {
            case Bus.Music: return _music;
            case Bus.Washing: return _wash;
            case Bus.Characters: return _animal;
            case Bus.Ambient: return _ambient;
            default: return _ui;
        }
    }

    /// <summary>Called every frame from GameController, even while paused, so
    /// fades never freeze mid-click.</summary>
    public static void Tick(float dt)
    {
        float step = Mathf.Max(0f, dt);
        float targetMusic = TargetMusic();
        _musicLevel = Mathf.MoveTowards(_musicLevel, targetMusic, step * 0.45f);
        if (_music != null) _music.volume = _musicLevel;

        float targetWash = _toolActive ? _toolTarget : 0f;
        _toolLevel = Mathf.MoveTowards(_toolLevel, targetWash, step * (_toolActive ? 2.6f : 1.9f));
        if (_wash != null)
        {
            _wash.volume = _toolLevel;
            if (_toolStopping && _toolLevel <= 0.002f)
            {
                _wash.Stop();
                _toolStopping = false;
            }
        }
    }

    private static float TargetMusic()
    {
        if (_muted || !_musicEnabled) return 0f;
        // Gentle ducking while a tool is running: the water remains clearly
        // audible without the music jumping when the finger stops.
        float duck = _toolActive ? 0.72f : 1f;
        return _masterVolume * _musicVolume * MusicBus * duck;
    }

    public static void SetMusicMode(string mode)
    {
        if (string.IsNullOrEmpty(mode) || mode == _musicMode || _music == null) return;
        _musicMode = mode;
        AudioClip clip = Music(mode);
        if (_music.clip == clip) return;
        _music.clip = clip;
        if (!_muted && _musicEnabled && !_music.isPlaying) _music.Play();
    }

    public static void StartTool(string id)
    {
        if (string.IsNullOrEmpty(id) || _wash == null || !_sfxEnabled || _muted) return;
        if (_toolId == id && _toolActive)
        {
            _toolStopping = false;
            return;
        }
        _toolId = id;
        _toolActive = true;
        _toolStopping = false;
        _variation = (_variation + 1) % 3;
        AudioClip clip = Loop(id + _variation);
        if (_wash.clip != clip)
        {
            // The previous loop is silent by the time a stage changes (the
            // controller fades it out), so this switch cannot click.
            _wash.Stop();
            _wash.clip = clip;
        }
        _wash.pitch = 0.97f + _variation * 0.025f;
        if (!_wash.isPlaying) _wash.Play();
        SetToolActivity(0.55f);
    }

    /// <summary>0 = finger stopped, 1 = fast continuous stroke.</summary>
    public static void SetToolActivity(float activity)
    {
        if (!_toolActive || !_sfxEnabled || _muted) return;
        _toolTarget = _sfxVolume * _masterVolume * WashingBus *
                      (0.38f + 0.62f * Mathf.Clamp01(activity));
    }

    public static void StopTool()
    {
        if (!_toolActive) return;
        _toolActive = false;
        _toolStopping = true;
        _toolTarget = 0f;
    }

    public static void Play(string name)
    {
        PlayOn(Bus.Ui, name, 0.75f);
    }

    public static void Play(string name, float volume)
    {
        PlayOn(Bus.Ui, name, volume);
    }

    public static void PlayUi(string name)
    {
        PlayOn(Bus.Ui, name, 0.62f);
    }

    public static void PlayAnimal(int level)
    {
        PlayOn(Bus.Characters, "animal_" + (Mathf.Abs(level) % 10), 0.85f);
    }

    public static void PlayAmbient(string name)
    {
        PlayOn(Bus.Ambient, name, 0.45f);
    }

    private static void PlayOn(Bus bus, string name, float volume)
    {
        AudioSource source = BusSource(bus);
        if (source == null || _muted || (bus != Bus.Music && !_sfxEnabled)) return;
        AudioClip clip;
        if (!Clips.TryGetValue(name, out clip))
        {
            clip = Create(name);
            Clips[name] = clip;
        }
        if (clip == null) return;
        // A tiny pitch variation keeps repeated splashes and clicks from
        // sounding like a machine without adding another source.
        source.pitch = 0.98f + ((_variation++ % 5) * 0.012f);
        source.PlayOneShot(clip, volume * BusGain(bus) * _sfxVolume * _masterVolume);
    }

    private static AudioClip Create(string name)
    {
        switch (name)
        {
            case "splash": return Tone(name, new[] { 420f, 680f }, 0.26f, 0.38f);
            case "foam": return Tone(name, new[] { 520f, 780f, 1040f }, 0.34f, 0.10f);
            case "rinse": return Tone(name, new[] { 900f, 1200f, 1500f }, 0.42f, 0.28f);
            case "scrub": return Tone(name, new[] { 260f, 330f }, 0.22f, 0.48f);
            case "dry": return Tone(name, new[] { 720f, 960f, 1280f }, 0.36f, 0.22f);
            case "star": return Arpeggio(name, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.55f);
            case "win": return Arpeggio(name, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.16f, 0.85f);
            case "horn": return Tone(name, new[] { 294f, 392f }, 0.30f, 0f);
            case "unlock": return Arpeggio(name, new[] { 392f, 523.25f, 659.25f }, 0.14f, 0.50f);
            case "click": return Tone(name, new[] { 660f }, 0.10f, 0f);
            case "pop": return Tone(name, new[] { 880f, 1120f }, 0.16f, 0f);
            case "depart": return Tone(name, new[] { 180f, 240f, 310f }, 0.70f, 0.18f);
            case "jump": return Arpeggio(name, new[] { 440f, 660f, 880f }, 0.08f, 0.30f);
        }
        if (name != null && name.StartsWith("animal_"))
        {
            int index = 0;
            int.TryParse(name.Substring(7), out index);
            return Animal(index);
        }
        return Tone(name, new[] { 880f }, 0.14f, 0f);
    }

    private static AudioClip Tone(string name, float[] frequencies, float duration, float noise)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));
            envelope *= envelope;
            float sample = 0f;
            for (int k = 0; k < frequencies.Length; k++)
                sample += Mathf.Sin(2f * Mathf.PI * frequencies[k] * t) / (k + 1);
            sample /= frequencies.Length;
            if (noise > 0f)
                sample = sample * (1f - noise) + (Mathf.PerlinNoise(t * 80f, 4.7f) * 2f - 1f) * noise;
            data[i] = sample * envelope * 0.32f;
        }
        return Clip(name, data);
    }

    private static AudioClip Arpeggio(string name, float[] notes, float spacing, float duration)
    {
        int count = Mathf.CeilToInt(SampleRate * (duration + spacing));
        var data = new float[count];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = Mathf.RoundToInt(n * spacing * SampleRate);
            int length = Mathf.RoundToInt(0.32f * SampleRate);
            for (int j = 0; j < length && start + j < count; j++)
            {
                float t = j / (float)SampleRate;
                float env = Mathf.Exp(-7f * t) * Mathf.Min(1f, t * 260f);
                data[start + j] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * env * 0.20f;
                data[start + j] += Mathf.Sin(4f * Mathf.PI * notes[n] * t) * env * 0.06f;
            }
        }
        return Clip(name, data);
    }

    private static AudioClip Animal(int index)
    {
        string name = "animal_" + index;
        float root = 520f + index * 37f;
        int count = Mathf.CeilToInt(SampleRate * 0.62f);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.62f));
            float chirp = root + Mathf.Sin(t * 18f + index) * 110f;
            data[i] = (Mathf.Sin(2f * Mathf.PI * chirp * t) * 0.7f +
                       Mathf.Sin(4f * Mathf.PI * chirp * t) * 0.2f) * envelope * 0.24f;
        }
        return Clip(name, data);
    }

    /// <summary>Original, wordless 8-second instrumental. Marimba-like notes,
    /// soft ukulele plucks and a light shaker. The last note decays before the
    /// loop point, so there is no click or perceptible seam.</summary>
    private static AudioClip Music(string mode)
    {
        string key = "music_" + mode;
        AudioClip cached;
        if (Clips.TryGetValue(key, out cached)) return cached;
        bool menu = mode == "menu";
        bool finale = mode == "finale";
        const float seconds = 8f;
        int count = Mathf.RoundToInt(SampleRate * seconds);
        var data = new float[count];
        float[] scale = menu
            ? new[] { 261.63f, 329.63f, 392f, 440f, 523.25f }
            : finale
                ? new[] { 392f, 493.88f, 587.33f, 659.25f, 783.99f }
                : new[] { 349.23f, 440f, 523.25f, 587.33f, 698.46f };
        int[] pattern = menu
            ? new[] { 0, 2, 4, 2, 1, 3, 2, 0, 0, 2, 3, 4, 2, 1, 0, 1 }
            : finale
                ? new[] { 0, 2, 4, 4, 3, 4, 2, 4, 0, 2, 3, 4, 2, 3, 4, 4 }
                : new[] { 0, 2, 4, 3, 2, 4, 1, 3, 0, 2, 4, 2, 3, 4, 2, 1 };
        float spacing = seconds / pattern.Length;
        for (int n = 0; n < pattern.Length; n++)
        {
            float freq = scale[pattern[n] % scale.Length];
            int start = Mathf.RoundToInt(n * spacing * SampleRate);
            for (int j = 0; j < SampleRate / 2 && start + j < count; j++)
            {
                float t = j / (float)SampleRate;
                float marimba = Mathf.Exp(-5.5f * t) * Mathf.Min(1f, t * 300f);
                float body = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f +
                             Mathf.Sin(4f * Mathf.PI * freq * t) * 0.18f +
                             Mathf.Sin(6f * Mathf.PI * freq * t) * 0.07f;
                float ukulele = Mathf.Exp(-11f * t) * Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.12f;
                data[start + j] += (body * marimba + ukulele) * 0.26f;
            }
            // A soft shaker on the offbeat, far below the wash sounds.
            int shake = start + Mathf.RoundToInt(spacing * 0.5f * SampleRate);
            for (int j = 0; j < SampleRate / 24 && shake + j < count; j++)
            {
                float t = j / (float)SampleRate;
                float env = Mathf.Exp(-55f * t);
                data[shake + j] += (Mathf.PerlinNoise(t * 310f, n * 1.7f) * 2f - 1f) * env * 0.035f;
            }
        }
        return Clip(key, data);
    }

    /// <summary>One of three short variations per tool. All partials complete
    /// an integer number of cycles inside the loop, so playback is seamless.</summary>
    private static AudioClip Loop(string id)
    {
        AudioClip cached;
        if (Loops.TryGetValue(id, out cached)) return cached;
        int variant = 0;
        if (id.Length > 0 && char.IsDigit(id[id.Length - 1]))
            int.TryParse(id.Substring(id.Length - 1), out variant);
        string tool = id.Length > 0 && char.IsDigit(id[id.Length - 1]) ? id.Substring(0, id.Length - 1) : id;
        const float seconds = 1.6f;
        int count = Mathf.RoundToInt(SampleRate * seconds);
        var data = new float[count];
        float baseFreq = 1f / seconds;
        float tone = tool == "scrub" ? 185f : (tool == "dry" ? 110f : 240f + variant * 31f);
        float air = tool == "foam" ? 0.55f : (tool == "scrub" ? 0.35f : 0.72f);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = 0f;
            for (int k = 1; k <= 19; k++)
            {
                float f = k * baseFreq * (tool == "dry" ? 1.5f : 1f);
                noise += Mathf.Sin(2f * Mathf.PI * f * t + (k + variant) * 1.71f) / Mathf.Sqrt(k);
            }
            noise /= 4.2f;
            float modulation = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * baseFreq * (2 + variant) * t);
            float hum = tool == "scrub"
                ? Mathf.Sin(2f * Mathf.PI * 13f * t) * 0.16f
                : Mathf.Sin(2f * Mathf.PI * tone * t) * 0.10f;
            data[i] = (noise * air * modulation + hum) * 0.34f;
        }
        AudioClip clip = Clip("loop_" + id, data);
        Loops[id] = clip;
        return clip;
    }

    private static AudioClip Clip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>Backwards-compatible short name used by the runtime scripts.</summary>
public static class Sfx
{
    public static bool Enabled
    {
        get { return AudioManager.Enabled; }
        set { AudioManager.Enabled = value; }
    }

    public static void Init(AudioSource source)
    {
        AudioManager.Init(source != null ? source.gameObject : null);
    }

    public static void Play(string name) { AudioManager.Play(name); }
}
