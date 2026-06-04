using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace backend.Services;

/// <summary>
/// RSA 密钥提供程序（Singleton）
/// 首次启动生成 RSA 2048 密钥对，持久化到文件
/// </summary>
public class RsaKeyProvider
{
    private readonly RSA _rsa;
    private readonly string _kid;

    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials SigningCredentials { get; }
    public string Kid => _kid;

    public RsaKeyProvider(IConfiguration configuration)
    {
        var keyPath = configuration.GetValue<string>("OAuth:RsaKeyStoragePath");
        if (string.IsNullOrEmpty(keyPath))
        {
            keyPath = Path.Combine(AppContext.BaseDirectory, "oauth-rsa-key.json");
        }

        _rsa = RSA.Create();
        var keySize = configuration.GetValue<int>("OAuth:RsaKeySizeBits", 2048);
        _rsa.KeySize = keySize;

        if (File.Exists(keyPath))
        {
            var json = File.ReadAllText(keyPath);
            var parameters = JsonSerializer.Deserialize<RsaParametersDto>(json);
            if (parameters != null)
            {
                _rsa.ImportParameters(parameters.ToRsaParameters());
            }
            else
            {
                GenerateAndSave(keyPath);
            }
        }
        else
        {
            GenerateAndSave(keyPath);
        }

        SecurityKey = new RsaSecurityKey(_rsa);
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
        _kid = Convert.ToBase64String(SHA256.HashData(_rsa.ExportRSAPublicKey()))
            .Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 32);
    }

    private void GenerateAndSave(string path)
    {
        var parameters = _rsa.ExportParameters(true);
        var dto = RsaParametersDto.FromRsaParameters(parameters);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 导出 JWKS 格式的公钥
    /// </summary>
    public Dictionary<string, object> GetJwksKey()
    {
        var parameters = _rsa.ExportParameters(false);
        var key = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = _kid,
            ["n"] = Base64UrlEncode(parameters.Modulus!),
            ["e"] = Base64UrlEncode(parameters.Exponent!),
        };
        return key;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private class RsaParametersDto
    {
        public string? D { get; set; }
        public string? DP { get; set; }
        public string? DQ { get; set; }
        public string? Exponent { get; set; }
        public string? InverseQ { get; set; }
        public string? Modulus { get; set; }
        public string? P { get; set; }
        public string? Q { get; set; }

        public RSAParameters ToRsaParameters()
        {
            return new RSAParameters
            {
                D = D != null ? Convert.FromBase64String(D) : null,
                DP = DP != null ? Convert.FromBase64String(DP) : null,
                DQ = DQ != null ? Convert.FromBase64String(DQ) : null,
                Exponent = Exponent != null ? Convert.FromBase64String(Exponent) : null,
                InverseQ = InverseQ != null ? Convert.FromBase64String(InverseQ) : null,
                Modulus = Modulus != null ? Convert.FromBase64String(Modulus) : null,
                P = P != null ? Convert.FromBase64String(P) : null,
                Q = Q != null ? Convert.FromBase64String(Q) : null,
            };
        }

        public static RsaParametersDto FromRsaParameters(RSAParameters p)
        {
            return new RsaParametersDto
            {
                D = p.D != null ? Convert.ToBase64String(p.D) : null,
                DP = p.DP != null ? Convert.ToBase64String(p.DP) : null,
                DQ = p.DQ != null ? Convert.ToBase64String(p.DQ) : null,
                Exponent = p.Exponent != null ? Convert.ToBase64String(p.Exponent) : null,
                InverseQ = p.InverseQ != null ? Convert.ToBase64String(p.InverseQ) : null,
                Modulus = p.Modulus != null ? Convert.ToBase64String(p.Modulus) : null,
                P = p.P != null ? Convert.ToBase64String(p.P) : null,
                Q = p.Q != null ? Convert.ToBase64String(p.Q) : null,
            };
        }
    }
}
