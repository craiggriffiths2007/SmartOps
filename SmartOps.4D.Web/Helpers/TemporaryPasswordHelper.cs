using System.Security.Cryptography;

public static class TemporaryPasswordHelper
{
    private const string Characters =
        "ABCDEFGHJKLMNPQRSTUVWXYZ" +
        "abcdefghijkmnopqrstuvwxyz" +
        "23456789";

    public static string Generate(int length = 10)
    {
        var password = new char[length];

        for (int i = 0; i < length; i++)
        {
            password[i] =
                Characters[
                    RandomNumberGenerator.GetInt32(
                        Characters.Length)];
        }

        return new string(password);
    }
}