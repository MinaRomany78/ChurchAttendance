using System.Security.Cryptography;

namespace ChurchAttendance.Security;

/// <summary>
/// PBKDF2 (SHA-256) with a random salt per code. Stored as "salt.hash.iterations" (all Base64 except the
/// iteration count), so a stored value never contains the plaintext code and cannot be reversed.
/// </summary>
public class AccessCodeHasher : IAccessCodeHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(code, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}.{Iterations}";
    }

    public bool Verify(string hash, string code)
    {
        var parts = hash.Split('.');
        if (parts.Length != 3) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            if (!int.TryParse(parts[2], out var iterations) || iterations <= 0) return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(code, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
