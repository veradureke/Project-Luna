using System;
using System.IO;
using UnityEngine;

// Converts between AudioClip and 16-bit PCM WAV byte[].
// FromAudioClip: used when uploading recorded mic audio to the server.
// ToAudioClip: used when playing back the WAV response the server sends.
public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        using (MemoryStream stream = new MemoryStream())
        {
            int hz = clip.frequency;
            int channels = clip.channels;
            int byteRate = hz * channels * 2;

            WriteString(stream, "RIFF");
            stream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4);
            WriteString(stream, "WAVE");
            WriteString(stream, "fmt ");
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);
            stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(hz), 0, 4);
            stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
            stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((short)16), 0, 2);
            WriteString(stream, "data");
            stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
            stream.Write(bytesData, 0, bytesData.Length);

            return stream.ToArray();
        }
    }

    // Parses a standard 16-bit PCM WAV file (which is what pyttsx3's
    // SAPI5 backend on Windows produces) into a playable AudioClip.
    // Assumes the fmt chunk is 16 bytes and comes before the data chunk,
    // true for the vast majority of WAV files including pyttsx3's output.
    public static AudioClip ToAudioClip(byte[] wavBytes)
    {
        if (wavBytes == null || wavBytes.Length < 44)
        {
            Debug.LogError("WavUtility: WAV data too short or null.");
            return null;
        }

        try
        {
            int channels = BitConverter.ToInt16(wavBytes, 22);
            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

            // Find the "data" chunk — its position can vary if there are
            // extra chunks (e.g. LIST) before it.
            int dataChunkStart = FindChunk(wavBytes, "data");
            if (dataChunkStart < 0)
            {
                Debug.LogError("WavUtility: Could not find 'data' chunk in WAV.");
                return null;
            }

            int dataSize = BitConverter.ToInt32(wavBytes, dataChunkStart + 4);
            int dataStart = dataChunkStart + 8;

            if (bitsPerSample != 16)
            {
                Debug.LogError($"WavUtility: Only 16-bit PCM WAV is supported, " +
                    $"got {bitsPerSample}-bit.");
                return null;
            }

            int sampleCount = dataSize / 2;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavBytes, dataStart + i * 2);
                samples[i] = sample / (float)short.MaxValue;
            }

            AudioClip clip = AudioClip.Create(
                "LunaResponse", sampleCount / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError("WavUtility: Failed to parse WAV data: " + e.Message);
            return null;
        }
    }

    private static int FindChunk(byte[] data, string chunkId)
    {
        byte[] target = System.Text.Encoding.ASCII.GetBytes(chunkId);
        for (int i = 12; i < data.Length - 4; i++)
        {
            if (data[i] == target[0] && data[i + 1] == target[1] &&
                data[i + 2] == target[2] && data[i + 3] == target[3])
                return i;
        }
        return -1;
    }

    private static void WriteString(Stream stream, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        stream.Write(bytes, 0, bytes.Length);
    }
}