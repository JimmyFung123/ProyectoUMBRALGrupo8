namespace UserService.Infrastructure.Security;

using System.Security.Cryptography;
using UserService.Application.Users;

/// <summary>
/// Cryptographically-secure password generator (<see cref="RandomNumberGenerator"/>).
/// Produces a 16-char password guaranteed to contain at least one lowercase,
/// uppercase, digit and symbol, so it satisfies common password policies and
/// Keycloak's first-login change. Visually-ambiguous characters (l, I, O, 0, 1)
/// are excluded so the password is easy to read in the email.
/// </summary>
public sealed class SecurePasswordGenerator : IPasswordGenerator
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";  // sin 'l'
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // sin 'I', 'O'
    private const string Digits = "23456789";                  // sin '0', '1'
    private const string Symbols = "!@#$%*?-_";
    private const int Length = 16;

    public string Generate()
    {
        var all = Lower + Upper + Digits + Symbols;
        var chars = new char[Length];

        // Garantiza al menos uno de cada categoría.
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < Length; i++)
            chars[i] = Pick(all);

        Shuffle(chars);
        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

    // Fisher–Yates con RNG criptográfico para no sesgar la posición de los
    // caracteres garantizados.
    private static void Shuffle(char[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
