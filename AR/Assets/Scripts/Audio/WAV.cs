using System;

public class WAV
{
    public float[] LeftChannel { get; private set; }
    public int ChannelCount { get; private set; }
    public int SampleCount { get; private set; }
    public int Frequency { get; private set; }

    public WAV(byte[] wav)
    {
        // NumChannels
        ChannelCount = BitConverter.ToInt16(wav, 22);
        Frequency = BitConverter.ToInt32(wav, 24);
        int pos = 44;

        int bytesPerSample = BitConverter.ToInt16(wav, 34) / 8;
        int sampleCount = (wav.Length - pos) / bytesPerSample;
        SampleCount = sampleCount / ChannelCount;

        LeftChannel = new float[SampleCount];

        int i = 0;
        while (pos < wav.Length)
        {
            short sample = BitConverter.ToInt16(wav, pos);
            LeftChannel[i] = sample / 32768f;
            pos += bytesPerSample * ChannelCount;
            i++;
        }
    }
}
