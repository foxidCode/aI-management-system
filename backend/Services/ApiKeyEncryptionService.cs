using System.Security.Cryptography;
using System.Text;

namespace backend.Services;

/// <summary>
/// API Key 加密服务：AES-256 对称加密，密钥不落盘时加密存储，不向前端暴露
/// </summary>
public class ApiKeyEncryptionService
{
    private readonly byte[] _key;

    public ApiKeyEncryptionService(IConfiguration configuration)
    {
        var encryptionKey = configuration["Encryption:Key"] ?? "DefaultAesKey_ChangeMe_In_Production_2024!";
        // 用 SHA256 将配置 Key 派生为 32 字节 AES-256 密钥
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
    }

    /// <summary>加密明文 API Key → Base64 密文（含 IV）</summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV (16 bytes) + CipherText → Base64
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>解密密文 → 明文 API Key</summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            // 前 16 字节是 IV
            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - 16];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
            Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // 解密失败：可能为旧版明文 Key，直接返回原值（兼容）
            return cipherText;
        }
    }
}
