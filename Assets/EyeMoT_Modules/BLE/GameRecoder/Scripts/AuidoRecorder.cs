using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AudioRecorder : MonoBehaviour
{
    private List<float> samples = new List<float>();
    private bool isRecording = false;
    private int channels = 2;
    private int sampleRate;

    public bool IsRecording => isRecording;

    private void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    public void StartRecording()
    {
        lock (samples)
        {
            samples.Clear();
        }
        isRecording = true;
        Debug.Log($"UnityAudioRecorder: start (sampleRate={sampleRate})");
    }

    public void StopAndSaveWav(string path)
    {
        if (!isRecording)
        {
            Debug.LogWarning("UnityAudioRecorder: not recording.");
            return;
        }

        isRecording = false;

        float[] data;
        lock (samples)
        {
            data = samples.ToArray();
        }

        Debug.Log($"UnityAudioRecorder: saving wav: {path} samples={data.Length}");

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        WriteWav(path, data, channels, sampleRate);
    }

    // AudioListener からの出力がここに全部流れてくる
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isRecording) return;

        this.channels = channels;

        lock (samples)
        {
            samples.AddRange(data);
        }
    }

    // float[-1,1] → 16bit PCM に変換して WAV 書き出し
    private void WriteWav(string filePath, float[] samples, int channels, int sampleRate)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fileStream))
        {
            int sampleCount = samples.Length;
            int byteCount = sampleCount * 2; // 16bit = 2byte

            // RIFF ヘッダ
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + byteCount);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt チャンク
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // サブチャンクサイズ
            writer.Write((short)1); // フォーマットID (1=PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2); // byte rate
            writer.Write((short)(channels * 2)); // block align
            writer.Write((short)16); // bits per sample

            // data チャンク
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(byteCount);

            // データ本体
            for (int i = 0; i < sampleCount; i++)
            {
                float f = Mathf.Clamp(samples[i], -1.0f, 1.0f);
                short s = (short)(f * short.MaxValue);
                writer.Write(s);
            }
        }
    }
}
