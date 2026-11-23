using System.Collections.Generic;
using UnityEngine;

/// <summary>Generates all sound effects in memory: zero audio files to download.</summary>
public static class Sfx
{
    private static AudioSource _source;
    private static bool _enabled = true;
    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

    public static bool Enabled
    {
        get { return _enabled; }
        set
        {
            _enabled = value;
            PlayerPrefs.SetInt(GameData.SoundKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Init(AudioSource source)
    {
        _source = source;
        _enabled = PlayerPrefs.GetInt(GameData.SoundKey, 1) == 1;
    }

    public static void Play(string name)
    {
        if (!_enabled || _source == null) return;
        AudioClip clip;
        if (!Clips.TryGetValue(name, out clip))
        {
            clip = Create(name);
            Clips[name] = clip;
        }
        if (clip != null) _source.PlayOneShot(clip, 0.75f);
    }

    private static AudioClip Create(string name)
    {
        switch (name)
        {
            case "splash": return Tone(name, new[] { 420f, 680f }, 0.26f, 0.38f);
            case "foam": return Tone(name, new[] { 520f, 780f, 1040f }, 0.34f, 0.10f);
            case "rinse": return Tone(name, new[] { 900f, 1200f, 1500f }, 0.42f, 0.28f);
            case "star": return Tone(name, new[] { 523f, 659f, 784f, 1046f }, 0.55f, 0f);
            case "win": return Tone(name, new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.85f, 0f);
            case "click": return Tone(name, new[] { 660f }, 0.10f, 0f);
            default: return Tone(name, new[] { 880f, 1120f }, 0.16f, 0f);
        }
    }

    private static AudioClip Tone(string name, float[] frequencies, float duration, float noise)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(rate * duration);
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
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
        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
