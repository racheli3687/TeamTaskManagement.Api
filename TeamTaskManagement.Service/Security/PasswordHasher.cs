// TeamTaskManagement.Service/Security/PasswordHasher.cs
using System.Security.Cryptography;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Security;

// PBKDF2-HMAC-SHA256, מובנה ב-.NET. פורמט: "iterations.salt.hash" (Base64).
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;          // 128 ביט
    private const int HashSize = 32;          // 256 ביט
    private const int Iterations = 600_000;   // המלצת OWASP ל-PBKDF2-HMAC-SHA256
    private const char Separator = '.';
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Iterations}{Separator}{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        string[] parts = passwordHash.Split(Separator);
        if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}