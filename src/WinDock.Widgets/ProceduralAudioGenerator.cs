using System.IO;
using WinDock.Core;

namespace WinDock.Widgets;

/// <summary>
/// High-performance procedural audio synthesizer that generates seamless, crossfaded 44.1kHz 16-bit stereo PCM WAV files.
/// Used when external audio assets are not pre-packaged or for instant offline soundscapes.
/// </summary>
public static class ProceduralAudioGenerator
{
    private const int SampleRate = 44100;
    private const double LoopDurationSeconds = 12.0;
    private const double CrossfadeSeconds = 1.0;

    public static void WarmUpInBackground()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            foreach (var track in WhiteNoiseTracks.All)
            {
                try
                {
                    GetOrGenerateAudioFile(track);
                }
                catch
                {
                }
            }
        });
    }

    public static string GetOrGenerateAudioFile(string trackId)
    {
        var localAppDataDir = ProductInfo.LocalData("audio");
        try
        {
            Directory.CreateDirectory(localAppDataDir);
        }
        catch
        {
        }

        var cachedPath = Path.Combine(localAppDataDir, $"{trackId}.wav");
        if (File.Exists(cachedPath))
        {
            var info = new FileInfo(cachedPath);
            if (info.Length > 44)
            {
                return cachedPath;
            }
        }

        // Check if an external asset is present in the application folder
        var appAssetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "WhiteNoise", $"{trackId}.mp3");
        if (File.Exists(appAssetPath))
        {
            return appAssetPath;
        }

        var appAssetWav = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "WhiteNoise", $"{trackId}.wav");
        if (File.Exists(appAssetWav))
        {
            return appAssetWav;
        }

        // Generate procedural audio into cache
        try
        {
            GenerateTrack(trackId, cachedPath);
        }
        catch
        {
        }

        return cachedPath;
    }

    public static void GenerateTrack(string trackId, string destinationPath)
    {
        var totalSamples = (int)(SampleRate * LoopDurationSeconds);
        var left = new float[totalSamples];
        var right = new float[totalSamples];

        switch (trackId)
        {
            case WhiteNoiseTracks.WhiteNoise:
                SynthesizeWhiteNoise(left, right);
                break;
            case WhiteNoiseTracks.PinkNoise:
                SynthesizePinkNoise(left, right);
                break;
            case WhiteNoiseTracks.BrownNoise:
                SynthesizeBrownNoise(left, right);
                break;
            case WhiteNoiseTracks.Rain:
                SynthesizeRain(left, right);
                break;
            case WhiteNoiseTracks.Thunder:
                SynthesizeThunder(left, right);
                break;
            case WhiteNoiseTracks.Waves:
                SynthesizeWaves(left, right);
                break;
            case WhiteNoiseTracks.Campfire:
                SynthesizeCampfire(left, right);
                break;
            case WhiteNoiseTracks.Wind:
                SynthesizeWind(left, right);
                break;
            case WhiteNoiseTracks.Cafe:
                SynthesizeCafe(left, right);
                break;
            case WhiteNoiseTracks.Keyboard:
                SynthesizeKeyboard(left, right);
                break;
            case WhiteNoiseTracks.Clock:
                SynthesizeClock(left, right);
                break;
            default:
                SynthesizePinkNoise(left, right);
                break;
        }

        ApplyEqualPowerCrossfade(left, right, (int)(SampleRate * CrossfadeSeconds));
        WriteWavFile(destinationPath, left, right, SampleRate);
    }

    private static void SynthesizeWhiteNoise(float[] left, float[] right)
    {
        var rngL = new Random(1001);
        var rngR = new Random(1002);
        for (var i = 0; i < left.Length; i++)
        {
            left[i] = (float)((rngL.NextDouble() * 2.0 - 1.0) * 0.35);
            right[i] = (float)((rngR.NextDouble() * 2.0 - 1.0) * 0.35);
        }
    }

    private static void SynthesizePinkNoise(float[] left, float[] right)
    {
        var rngL = new Random(2001);
        var rngR = new Random(2002);

        double b0L = 0, b1L = 0, b2L = 0, b3L = 0, b4L = 0, b5L = 0, b6L = 0;
        double b0R = 0, b1R = 0, b2R = 0, b3R = 0, b4R = 0, b5R = 0, b6R = 0;

        for (var i = 0; i < left.Length; i++)
        {
            var wL = rngL.NextDouble() * 2.0 - 1.0;
            b0L = 0.99886 * b0L + wL * 0.0555179;
            b1L = 0.99332 * b1L + wL * 0.0750759;
            b2L = 0.96900 * b2L + wL * 0.1538520;
            b3L = 0.86650 * b3L + wL * 0.3104856;
            b4L = 0.55000 * b4L + wL * 0.5329522;
            b5L = -0.7616 * b5L - wL * 0.0168980;
            left[i] = (float)((b0L + b1L + b2L + b3L + b4L + b5L + b6L + wL * 0.5362) * 0.16);
            b6L = wL * 0.115926;

            var wR = rngR.NextDouble() * 2.0 - 1.0;
            b0R = 0.99886 * b0R + wR * 0.0555179;
            b1R = 0.99332 * b1R + wR * 0.0750759;
            b2R = 0.96900 * b2R + wR * 0.1538520;
            b3R = 0.86650 * b3R + wR * 0.3104856;
            b4R = 0.55000 * b4R + wR * 0.5329522;
            b5R = -0.7616 * b5R - wR * 0.0168980;
            right[i] = (float)((b0R + b1R + b2R + b3R + b4R + b5R + b6R + wR * 0.5362) * 0.16);
            b6R = wR * 0.115926;
        }
    }

    private static void SynthesizeBrownNoise(float[] left, float[] right)
    {
        var rngL = new Random(3001);
        var rngR = new Random(3002);
        double brownL = 0.0;
        double brownR = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            var wL = rngL.NextDouble() * 2.0 - 1.0;
            brownL = (brownL * 0.985) + (wL * 0.06);
            left[i] = (float)(brownL * 0.45);

            var wR = rngR.NextDouble() * 2.0 - 1.0;
            brownR = (brownR * 0.985) + (wR * 0.06);
            right[i] = (float)(brownR * 0.45);
        }
    }

    private static void SynthesizeRain(float[] left, float[] right)
    {
        // Pink noise background base (hiss of rainfall)
        SynthesizePinkNoise(left, right);
        for (var i = 0; i < left.Length; i++)
        {
            left[i] *= 0.5f;
            right[i] *= 0.5f;
        }

        // Add Poisson raindrop impulses (random high frequency decaying ping)
        var rng = new Random(4001);
        for (var i = 0; i < left.Length - 1000; i++)
        {
            if (rng.NextDouble() < 0.0035) // Random raindrop
            {
                var dropLength = rng.Next(150, 450);
                var pan = (float)rng.NextDouble();
                var amp = (float)(0.2 + rng.NextDouble() * 0.35);
                var freq = 1200.0 + rng.NextDouble() * 1800.0;

                for (var d = 0; d < dropLength && (i + d) < left.Length; d++)
                {
                    var env = Math.Exp(-d * 0.015);
                    var val = (float)(Math.Sin(2.0 * Math.PI * freq * d / SampleRate) * env * amp);
                    left[i + d] += val * (1f - pan);
                    right[i + d] += val * pan;
                }
            }
        }
    }

    private static void SynthesizeThunder(float[] left, float[] right)
    {
        var rng = new Random(5001);
        double brown = 0.0;
        var subbassPhase = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            var t = (double)i / SampleRate;
            // Modulation envelope creates rolling rumble bursts
            var mod1 = Math.Max(0.0, Math.Sin(2.0 * Math.PI * 0.16 * t) * 0.7);
            var mod2 = Math.Max(0.0, Math.Sin(2.0 * Math.PI * 0.08 * t + 1.2) * 0.8);
            var env = Math.Min(1.0, mod1 + mod2);

            var w = rng.NextDouble() * 2.0 - 1.0;
            brown = (brown * 0.992) + (w * 0.035);

            // Sub-bass rumble around 45Hz - 70Hz
            subbassPhase += 2.0 * Math.PI * (50.0 + 15.0 * Math.Sin(t * 1.5)) / SampleRate;
            var subbass = Math.Sin(subbassPhase) * 0.25;

            var sample = (float)((brown + subbass) * env * 0.75);
            left[i] = sample;
            right[i] = sample * 0.95f;
        }
    }

    private static void SynthesizeWaves(float[] left, float[] right)
    {
        var rngL = new Random(6001);
        var rngR = new Random(6002);
        double brownL = 0.0;
        double brownR = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            var t = (double)i / SampleRate;
            // 4.5 second swell and wash cycle
            var swell = Math.Pow(0.5 + 0.5 * Math.Sin(2.0 * Math.PI * (t / 4.5)), 2.0);
            var wash = Math.Pow(0.5 + 0.5 * Math.Sin(2.0 * Math.PI * (t / 4.5) + 0.4), 1.5);

            var wL = rngL.NextDouble() * 2.0 - 1.0;
            brownL = (brownL * 0.982) + (wL * 0.06);

            var wR = rngR.NextDouble() * 2.0 - 1.0;
            brownR = (brownR * 0.982) + (wR * 0.06);

            left[i] = (float)(brownL * (0.35 + 0.65 * swell) * 0.5);
            right[i] = (float)(brownR * (0.35 + 0.65 * wash) * 0.5);
        }
    }

    private static void SynthesizeCampfire(float[] left, float[] right)
    {
        // Low warm air background
        SynthesizeBrownNoise(left, right);
        for (var i = 0; i < left.Length; i++)
        {
            left[i] *= 0.25f;
            right[i] *= 0.25f;
        }

        // Random wood crackle impulses & pops
        var rng = new Random(7001);
        for (var i = 0; i < left.Length - 400; i++)
        {
            if (rng.NextDouble() < 0.0018) // Crackle trigger
            {
                var popLength = rng.Next(40, 180);
                var pan = (float)rng.NextDouble();
                var amp = (float)(0.4 + rng.NextDouble() * 0.5);
                var snapFreq = 2200.0 + rng.NextDouble() * 3000.0;

                for (var d = 0; d < popLength && (i + d) < left.Length; d++)
                {
                    var env = Math.Exp(-d * 0.05);
                    var val = (float)((rng.NextDouble() * 2.0 - 1.0) * env * amp
                        + Math.Sin(2.0 * Math.PI * snapFreq * d / SampleRate) * env * (amp * 0.4));
                    left[i + d] += val * (1f - pan);
                    right[i + d] += val * pan;
                }
            }
        }
    }

    private static void SynthesizeWind(float[] left, float[] right)
    {
        var rng = new Random(8001);
        double filterL = 0.0;
        double filterR = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            var t = (double)i / SampleRate;
            // Gusting LFO between 0.1Hz and 0.25Hz
            var gust = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 0.12 * t);
            var cutoff = 0.04 + (0.09 * gust); // Resonant low/band pass

            var wL = rng.NextDouble() * 2.0 - 1.0;
            filterL += cutoff * (wL - filterL);

            var wR = rng.NextDouble() * 2.0 - 1.0;
            filterR += cutoff * (wR - filterR);

            var amp = 0.4 + (0.5 * gust);
            left[i] = (float)(filterL * amp * 0.55);
            right[i] = (float)(filterR * amp * 0.55);
        }
    }

    private static void SynthesizeCafe(float[] left, float[] right)
    {
        // Warm diffuse ambient room tone
        SynthesizePinkNoise(left, right);
        for (var i = 0; i < left.Length; i++)
        {
            left[i] *= 0.35f;
            right[i] *= 0.35f;
        }

        // Occasional distant cup/saucer clink
        var rng = new Random(9001);
        for (var i = 0; i < left.Length - 1200; i++)
        {
            if (rng.NextDouble() < 0.0004) // Clink trigger
            {
                var clinkLength = rng.Next(400, 900);
                var pan = (float)rng.NextDouble();
                var amp = 0.25f;
                var toneFreq = 2400.0 + rng.Next(0, 3) * 600.0;

                for (var d = 0; d < clinkLength && (i + d) < left.Length; d++)
                {
                    var env = Math.Exp(-d * 0.012);
                    var val = (float)(Math.Sin(2.0 * Math.PI * toneFreq * d / SampleRate) * env * amp);
                    left[i + d] += val * (1f - pan);
                    right[i + d] += val * pan;
                }
            }
        }
    }

    private static void SynthesizeKeyboard(float[] left, float[] right)
    {
        // Background room warmth
        for (var i = 0; i < left.Length; i++)
        {
            left[i] = 0;
            right[i] = 0;
        }

        var rng = new Random(10001);
        var clickStep = (int)(SampleRate * 0.22); // average key interval
        var nextClick = (int)(SampleRate * 0.1);

        for (var i = 0; i < left.Length - 300; i++)
        {
            if (i >= nextClick)
            {
                var pan = (float)(0.3 + rng.NextDouble() * 0.4);
                var amp = (float)(0.3 + rng.NextDouble() * 0.35);
                var keyFreq = 1800.0 + rng.NextDouble() * 1200.0;
                var len = rng.Next(60, 160);

                for (var d = 0; d < len && (i + d) < left.Length; d++)
                {
                    var env = Math.Exp(-d * 0.04);
                    var val = (float)((rng.NextDouble() * 2.0 - 1.0) * env * amp
                        + Math.Sin(2.0 * Math.PI * keyFreq * d / SampleRate) * env * (amp * 0.3));
                    left[i + d] += val * (1f - pan);
                    right[i + d] += val * pan;
                }

                nextClick = i + (int)(clickStep * (0.6 + rng.NextDouble() * 0.8));
            }
        }
    }

    private static void SynthesizeClock(float[] left, float[] right)
    {
        for (var i = 0; i < left.Length; i++)
        {
            left[i] = 0;
            right[i] = 0;
        }

        var tickInterval = SampleRate; // 1 second exactly
        for (var tick = 0; tick * tickInterval < left.Length - 200; tick++)
        {
            var start = tick * tickInterval;
            var isTick = tick % 2 == 0;
            var freq = isTick ? 1200.0 : 850.0;
            var len = 120;

            for (var d = 0; d < len && (start + d) < left.Length; d++)
            {
                var env = Math.Exp(-d * 0.05);
                var val = (float)(Math.Sin(2.0 * Math.PI * freq * d / SampleRate) * env * 0.4);
                left[start + d] += val;
                right[start + d] += val;
            }
        }
    }

    private static void ApplyEqualPowerCrossfade(float[] left, float[] right, int fadeSamples)
    {
        var len = left.Length;
        if (len <= fadeSamples * 2 || fadeSamples <= 0)
        {
            return;
        }

        for (var i = 0; i < fadeSamples; i++)
        {
            var t = (double)i / fadeSamples;
            var fadeIn = (float)Math.Sin(t * Math.PI * 0.5);
            var fadeOut = (float)Math.Cos(t * Math.PI * 0.5);

            var tailIndex = len - fadeSamples + i;

            // Blend head
            left[i] = (left[i] * fadeIn) + (left[tailIndex] * fadeOut);
            right[i] = (right[i] * fadeIn) + (right[tailIndex] * fadeOut);

            // Match tail to head
            left[tailIndex] = left[i];
            right[tailIndex] = right[i];
        }
    }

    private static void WriteWavFile(string path, float[] left, float[] right, int sampleRate)
    {
        var numSamples = left.Length;
        var numChannels = (short)2;
        var bitsPerSample = (short)16;
        var bytesPerSample = bitsPerSample / 8;
        var subChunk2Size = numSamples * numChannels * bytesPerSample;
        var chunkSize = 36 + subChunk2Size;
        var byteRate = sampleRate * numChannels * bytesPerSample;
        var blockAlign = (short)(numChannels * bytesPerSample);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write("RIFF"u8.ToArray());
        writer.Write(chunkSize);
        writer.Write("WAVE"u8.ToArray());

        // fmt subchunk
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // Subchunk1Size for PCM
        writer.Write((short)1); // AudioFormat: 1 = PCM
        writer.Write(numChannels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // data subchunk
        writer.Write("data"u8.ToArray());
        writer.Write(subChunk2Size);

        // Write interleaved 16-bit PCM samples
        for (var i = 0; i < numSamples; i++)
        {
            var sampleL = (short)Math.Clamp(left[i] * 32767f, short.MinValue, short.MaxValue);
            var sampleR = (short)Math.Clamp(right[i] * 32767f, short.MinValue, short.MaxValue);
            writer.Write(sampleL);
            writer.Write(sampleR);
        }

        writer.Flush();
    }
}
