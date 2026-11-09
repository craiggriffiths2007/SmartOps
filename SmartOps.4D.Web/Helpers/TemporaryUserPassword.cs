using System.Security.Cryptography;

namespace SmartOps.Web.Helpers;

public static class TemporaryUserPassword
{
    private static readonly string[] Adjectives = ["Blue", "Green", "Silver", "Golden", "Bright", "Happy", "Sunny", "Quiet", "Fresh", "Swift", "Lucky", "Clear"];
    private static readonly string[] Nouns = ["River", "Garden", "Forest", "Meadow", "Bridge", "Window", "Robin", "Oak", "Willow", "Sunrise", "Harbour", "Mountain"];

    public static string Generate() => Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)]
        + Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)]
        + RandomNumberGenerator.GetInt32(1000, 10000);
}
