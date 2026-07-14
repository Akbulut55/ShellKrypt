namespace ShellKrypt.Infrastructure.Authenticator;

internal static class Base32Codec
{
    internal static byte[] Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Authenticator secret is required.");

        var buffer = new List<byte>(value.Length * 5 / 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var ch in value)
        {
            var mapped = ch switch
            {
                >= 'A' and <= 'Z' => ch - 'A',
                >= '2' and <= '7' => ch - '2' + 26,
                '=' => -1,
                _ => throw new InvalidOperationException("Authenticator secret must be valid Base32.")
            };

            if (mapped < 0)
                break;

            bitBuffer = (bitBuffer << 5) | mapped;
            bitCount += 5;

            while (bitCount >= 8)
            {
                bitCount -= 8;
                buffer.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        if (buffer.Count == 0)
            throw new InvalidOperationException("Authenticator secret must be valid Base32.");

        return buffer.ToArray();
    }
}
