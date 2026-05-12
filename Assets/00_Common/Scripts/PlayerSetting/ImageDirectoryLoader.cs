using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EyeMoT
{
    public static class ImageDirectoryLoader
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

        public static async Task<List<CompressedImageInfo>> LoadResizedImagesAsync(
            string directoryPath,
            int maxLongSide,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Directory path is null or empty.", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            if (maxLongSide <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLongSide), "Max long side must be greater than 0.");

            string[] filePaths = Directory.GetFiles(directoryPath);
            List<CompressedImageInfo> results = new List<CompressedImageInfo>();

            foreach (string filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSupportedImage(filePath))
                    continue;

                byte[] sourceBytes = await ReadAllBytesAsync(filePath, cancellationToken);
                await Task.Yield();

                CompressedImageInfo imageInfo = CreateResizedImageInfo(
                    sourceBytes,
                    filePath,
                    maxLongSide);

                results.Add(imageInfo);

                await Task.Yield();
            }

            return results;
        }

        private static bool IsSupportedImage(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            foreach (string supportedExtension in SupportedExtensions)
            {
                if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string filePath, CancellationToken cancellationToken)
        {
            using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            if (stream.Length > int.MaxValue)
                throw new IOException($"File is too large: {filePath}");

            byte[] buffer = new byte[(int)stream.Length];
            int offset = 0;

            while (offset < buffer.Length)
            {
                int readSize = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                if (readSize == 0)
                    break;

                offset += readSize;
            }

            return buffer;
        }

        private static CompressedImageInfo CreateResizedImageInfo(byte[] sourceBytes, string filePath, int maxLongSide)
        {
            string fileName = Path.GetFileName(filePath);
            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!loadedTexture.LoadImage(sourceBytes, markNonReadable: false))
            {
                UnityEngine.Object.Destroy(loadedTexture);
                throw new InvalidDataException($"Failed to load image: {fileName}");
            }

            Vector2Int resizedDimensions = CalculateResizedDimensions(loadedTexture.width, loadedTexture.height, maxLongSide);
            Texture2D resizedTexture = ResizeTexture(loadedTexture, resizedDimensions.x, resizedDimensions.y);
            bool preserveAlpha = ShouldPreserveAlpha(filePath, loadedTexture);
            byte[] encodedBytes = preserveAlpha
                ? resizedTexture.EncodeToPNG()
                : resizedTexture.EncodeToJPG(100);

            UnityEngine.Object.Destroy(loadedTexture);
            return new CompressedImageInfo(fileName, filePath, resizedTexture, encodedBytes);
        }

        private static Vector2Int CalculateResizedDimensions(int width, int height, int maxLongSide)
        {
            int longSide = Mathf.Max(width, height);
            if (longSide <= maxLongSide)
                return new Vector2Int(width, height);

            float scale = (float)maxLongSide / longSide;
            int resizedWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int resizedHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            return new Vector2Int(resizedWidth, resizedHeight);
        }

        private static Texture2D ResizeTexture(Texture2D sourceTexture, int width, int height)
        {
            RenderTexture temporaryRenderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                Graphics.Blit(sourceTexture, temporaryRenderTexture);
                RenderTexture.active = temporaryRenderTexture;

                Texture2D resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resizedTexture.Apply();
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(temporaryRenderTexture);
            }
        }

        private static bool ShouldPreserveAlpha(string filePath, Texture2D texture)
        {
            string extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                return false;

            Color32[] pixels = texture.GetPixels32();
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < byte.MaxValue)
                    return true;
            }

            return true;
        }
    }

    [Serializable]
    public sealed class CompressedImageInfo
    {
        public string FileName { get; }
        public string FilePath { get; }
        public Texture2D Texture { get; }
        public byte[] EncodedBytes { get; }

        public CompressedImageInfo(string fileName, string filePath, Texture2D texture, byte[] encodedBytes)
        {
            FileName = fileName;
            FilePath = filePath;
            Texture = texture;
            EncodedBytes = encodedBytes;
        }
    }
}
