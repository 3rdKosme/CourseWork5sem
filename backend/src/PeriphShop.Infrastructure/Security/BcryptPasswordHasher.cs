using PeriphShop.Application.Abstractions;

namespace PeriphShop.Infrastructure.Security;

/// <summary>BCrypt с cost = 11 (раздел 9 ТЗ, FR-10).</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Повреждённый или устаревший формат хэша не должен приводить к 500.
            return false;
        }
    }
}
