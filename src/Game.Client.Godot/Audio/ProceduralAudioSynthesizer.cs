using Godot;

namespace Game.Client.Godot.Audio;

public static class ProceduralAudioSynthesizer
{
    private const int SampleRate = 22050;

    public static AudioStreamWav CreateFootstep()
    {
        float duration = 0.08f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Exp(-progress * 18.0f); // Fast decay
            float wave = MathF.Sin(2f * MathF.PI * 120f * t) * 0.7f +
                         MathF.Sin(2f * MathF.PI * 60f * t) * 0.3f;
            float noise = (Random.Shared.NextSingle() * 2f - 1f) * 0.15f;
            float sample = (wave + noise) * envelope * 0.45f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateLockClick()
    {
        float duration = 0.12f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Exp(-progress * 22.0f);
            float freq = 1200f + (1f - progress) * 800f; // Metallic sweep
            float wave = MathF.Sin(2f * MathF.PI * freq * t) * 0.8f +
                         MathF.Sin(2f * MathF.PI * (freq * 2.1f) * t) * 0.2f;
            float sample = wave * envelope * 0.6f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreatePaperFlutter()
    {
        float duration = 0.18f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float progress = (float)i / totalSamples;
            float envelope = MathF.Sin(MathF.PI * progress) * MathF.Exp(-progress * 4f);
            float noise = (Random.Shared.NextSingle() * 2f - 1f) * 0.4f;
            float sample = noise * envelope;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateFuseboxBuzz()
    {
        float duration = 0.25f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Exp(-progress * 6.0f);
            float wave = MathF.Sin(2f * MathF.PI * 60f * t) >= 0 ? 0.6f : -0.6f; // Square buzz
            float noise = (Random.Shared.NextSingle() * 2f - 1f) * 0.2f;
            float sample = (wave + noise) * envelope * 0.5f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateTerminalBeep()
    {
        float duration = 0.15f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Sin(MathF.PI * progress);
            float wave = MathF.Sin(2f * MathF.PI * 1800f * t);
            float sample = wave * envelope * 0.4f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateDialogueChime()
    {
        float duration = 0.35f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Exp(-progress * 7.0f);
            float wave = MathF.Sin(2f * MathF.PI * 587.33f * t) * 0.6f + // D5
                         MathF.Sin(2f * MathF.PI * 880.00f * t) * 0.4f;   // A5
            float sample = wave * envelope * 0.5f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateAnomalyWarp()
    {
        float duration = 0.9f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Sin(MathF.PI * progress);
            float freq = 340f - progress * 240f; // Descending glide
            float tremolo = 1f + 0.3f * MathF.Sin(2f * MathF.PI * 8f * t);
            float wave = (MathF.Sin(2f * MathF.PI * freq * t) * 0.6f +
                          MathF.Sin(2f * MathF.PI * (freq * 1.414f) * t) * 0.4f) * tremolo;
            float sample = wave * envelope * 0.65f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    public static AudioStreamWav CreateClimaxAlert()
    {
        float duration = 1.2f;
        int totalSamples = (int)(SampleRate * duration);
        byte[] data = new byte[totalSamples * 2];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / totalSamples;
            float envelope = MathF.Exp(-progress * 3.5f);
            float chord = MathF.Sin(2f * MathF.PI * 440f * t) * 0.4f +    // A4
                          MathF.Sin(2f * MathF.PI * 554.37f * t) * 0.35f + // C#5
                          MathF.Sin(2f * MathF.PI * 659.25f * t) * 0.25f;  // E5
            float sample = chord * envelope * 0.7f;

            short pcm = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(pcm & 0xFF);
            data[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return BuildStream(data);
    }

    private static AudioStreamWav BuildStream(byte[] data) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = SampleRate,
        Stereo = false,
        Data = data,
    };
}
