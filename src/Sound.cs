using System;
using System.IO;
using System.Media;

// 알림음을 코드로 합성해 재생한다. 음량(0~100)은 파형의 진폭에 직접 반영된다.
static class Sound
{
    const int Rate = 44100;
    static SoundPlayer current;

    // 집중 시작: 밝고 올라가는 3음 / 휴식 시작: 부드럽고 내려가는 2음
    public static void Play(bool focusStart, int volume)
    {
        try
        {
            if (current != null) { try { current.Stop(); current.Dispose(); } catch (Exception) { } }
            current = new SoundPlayer(new MemoryStream(Build(focusStart, volume)));
            current.Play();
        }
        catch (Exception) { }
    }

    static byte[] Build(bool focusStart, int volume)
    {
        double[][] notes = focusStart
            ? new[] { new[] { 0.00, 660, 0.26 }, new[] { 0.16, 880, 0.26 }, new[] { 0.32, 1320, 0.30 } }
            : new[] { new[] { 0.00, 880, 0.55 }, new[] { 0.30, 587, 0.65 } };

        double end = 0;
        foreach (double[] n in notes) end = Math.Max(end, n[0] + n[2]);
        int count = (int)((end + 0.05) * Rate);
        double[] mix = new double[count];

        foreach (double[] n in notes)
        {
            int start = (int)(n[0] * Rate), len = (int)(n[2] * Rate);
            for (int i = 0; i < len && start + i < count; i++)
            {
                double t = (double)i / Rate;
                double phase = 2 * Math.PI * n[1] * t;
                double wave = focusStart ? 2 / Math.PI * Math.Asin(Math.Sin(phase)) : Math.Sin(phase);
                double env = t < 0.02 ? t / 0.02 : Math.Exp(-5.0 * (t - 0.02) / (n[2] - 0.02));
                mix[start + i] += wave * env;
            }
        }

        double amp = Math.Max(0, Math.Min(100, volume)) / 100.0 * 0.9;
        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
        int bytes = count * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
        foreach (double v in mix)
            w.Write((short)(Math.Max(-1, Math.Min(1, v * amp)) * 32767));
        w.Flush();
        return ms.ToArray();
    }
}
