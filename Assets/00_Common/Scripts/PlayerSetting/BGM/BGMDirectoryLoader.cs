using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EyeMoT
{
    public static class BGMDirectoryLoader
    {
        private static readonly Dictionary<string, AudioType> SupportedAudioTypes =
            new Dictionary<string, AudioType>(StringComparer.OrdinalIgnoreCase)
            {
                { ".mp3", AudioType.MPEG },
                { ".wav", AudioType.WAV },
                { ".ogg", AudioType.OGGVORBIS }
            };

        public static async Task<List<CompressedBGMInfo>> LoadAllBgmsAsync(
            string directoryPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Directory path is null or empty.", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string[] files = Directory.GetFiles(directoryPath);
            List<CompressedBGMInfo> results = new List<CompressedBGMInfo>();

            foreach (string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryGetAudioType(filePath, out AudioType audioType))
                    continue;

                try
                {
                    CompressedBGMInfo info =
                        await LoadAudioClipAsync(filePath, audioType, cancellationToken);

                    results.Add(info);

                    Debug.Log($"音源読み込み成功: {info.FileName}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"音源読み込み失敗: {filePath}\n{ex.Message}");
                }
            }

            return results;
        }

        public static bool TryGetAudioType(string filePath, out AudioType audioType)
        {
            string extension = Path.GetExtension(filePath);

            return SupportedAudioTypes.TryGetValue(extension, out audioType);
        }

        public static async Task<CompressedBGMInfo> LoadAudioClipAsync(
            string filePath,
            AudioType audioType,
            CancellationToken cancellationToken = default)
        {
            string url = ToFileUrl(filePath);

            using UnityWebRequest request =
                UnityWebRequestMultimedia.GetAudioClip(url, audioType);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(request.error);
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = Path.GetFileNameWithoutExtension(filePath);

            byte[] encodedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            return new CompressedBGMInfo(
                fileName: Path.GetFileName(filePath),
                filePath: filePath,
                audioClip: clip,
                encodedBytes: encodedBytes
            );
        }

        private static string ToFileUrl(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath).Replace("\\", "/");

            return "file:///" + fullPath;
        }
    }

    [Serializable]
    public sealed class CompressedBGMInfo
    {
        public string FileName { get; }
        public string FilePath { get; }
        public AudioClip AudioClip { get; }
        public byte[] EncodedBytes { get; }

        public CompressedBGMInfo(
            string fileName,
            string filePath,
            AudioClip audioClip,
            byte[] encodedBytes)
        {
            FileName = fileName;
            FilePath = filePath;
            AudioClip = audioClip;
            EncodedBytes = encodedBytes;
        }
    }
}