public static class CustomerReferenceHelper
{
    private const long Modulus = 100_000_000;
    private const long Multiplier = 73_856_093;
    private const long Offset = 41_729_561;

    private static readonly long InverseMultiplier =
        ModularInverse(Multiplier, Modulus);

    public static string Encode(int contractNumber)
    {
        if (contractNumber < 0 || contractNumber >= Modulus)
            throw new ArgumentOutOfRangeException(nameof(contractNumber));

        long encoded =
            ((contractNumber * Multiplier) + Offset) % Modulus;

        return encoded.ToString("D8");
    }

    public static int Decode(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            reference.Length != 8 ||
            !long.TryParse(reference, out long encoded))
        {
            throw new ArgumentException(
                "Reference must be an 8-digit number.",
                nameof(reference));
        }

        long value =
            ((encoded - Offset + Modulus) * InverseMultiplier)
            % Modulus;

        return (int)value;
    }

    private static long ModularInverse(long a, long modulus)
    {
        long t = 0;
        long newT = 1;
        long r = modulus;
        long newR = a;

        while (newR != 0)
        {
            long quotient = r / newR;

            (t, newT) =
                (newT, t - quotient * newT);

            (r, newR) =
                (newR, r - quotient * newR);
        }

        if (r > 1)
            throw new InvalidOperationException(
                "Multiplier has no modular inverse.");

        if (t < 0)
            t += modulus;

        return t;
    }
}