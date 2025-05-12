using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace desktopScanner.Services;

public class SecureChannelService
{
    private readonly byte[] _encryptionKey;

    public SecureChannelService(byte[] encryptionKey)
    {
        _encryptionKey = encryptionKey ?? throw new ArgumentNullException(nameof(encryptionKey));
    }

    public async Task<byte[]> EncryptAndCompressAsync(string jsonData)
    {
        // 1. Сжатие данных с помощью GZip
        byte[] compressedData;
        using (var uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData)))
        using (var compressedStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                await uncompressedStream.CopyToAsync(gzipStream);
            }
            compressedData = compressedStream.ToArray();
        }

        // 2. Шифрование с помощью AES-256-GCM
        byte[] encryptedData;
        byte[] tag = new byte[16]; // Тег аутентификации GCM
        byte[] nonce = new byte[12]; // Нонс для GCM

        using (var aes = new AesGcm(_encryptionKey))
        {
            encryptedData = new byte[compressedData.Length];
            RandomNumberGenerator.Fill(nonce); // Генерация случайного нонса
            aes.Encrypt(nonce, compressedData, encryptedData, tag);
        }

        // 3. Объединение нонса, тега и зашифрованных данных в один массив
        var result = new byte[nonce.Length + tag.Length + encryptedData.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(encryptedData, 0, result, nonce.Length + tag.Length, encryptedData.Length);

        return result;
    }
}