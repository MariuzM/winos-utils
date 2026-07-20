using System.Text;

namespace WinVrr;

internal static class Edid
{
    public const int BlockSize = 128;

    private static ReadOnlySpan<byte> Header => [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    public static bool HasValidHeader(ReadOnlySpan<byte> block)
    {
        return block.Length >= BlockSize && block[..8].SequenceEqual(Header);
    }

    public static string? GetName(ReadOnlySpan<byte> block)
    {
        int offset = FindDescriptor(block, 0xFC);
        if (offset < 0)
            return null;

        ReadOnlySpan<byte> text = block.Slice(offset + 5, 13);
        int end = text.IndexOf((byte)0x0A);
        if (end < 0)
            end = text.Length;

        return Encoding.ASCII.GetString(text[..end]).Trim();
    }

    public static (int Min, int Max)? GetVerticalRange(ReadOnlySpan<byte> block)
    {
        int offset = FindDescriptor(block, 0xFD);
        if (offset < 0)
            return null;

        int flags = block[offset + 4] & 0b11;
        int min = block[offset + 5] + (flags == 0b11 ? 255 : 0);
        int max = block[offset + 6] + (flags >= 0b10 ? 255 : 0);
        return (min, max);
    }

    public static string? SetVerticalRange(byte[] block, int min, int max)
    {
        int offset = FindDescriptor(block, 0xFD);
        if (offset < 0)
            return "no range limits descriptor in EDID block 0";

        if (min < 1 || max > 510 || min >= max)
            return "range must satisfy 1 <= min < max <= 510";

        int flags =
            min > 255 ? 0b11
            : max > 255 ? 0b10
            : 0b00;
        block[offset + 4] = (byte)((block[offset + 4] & ~0b11) | flags);
        block[offset + 5] = (byte)(min > 255 ? min - 255 : min);
        block[offset + 6] = (byte)(max > 255 ? max - 255 : max);
        FixChecksum(block);
        return null;
    }

    public static void FixChecksum(byte[] block)
    {
        int sum = 0;
        for (int i = 0; i < BlockSize - 1; i++)
            sum += block[i];

        block[BlockSize - 1] = (byte)((256 - (sum & 0xFF)) & 0xFF);
    }

    private static int FindDescriptor(ReadOnlySpan<byte> block, byte tag)
    {
        for (int i = 54; i <= 108; i += 18)
        {
            if (block[i] == 0 && block[i + 1] == 0 && block[i + 2] == 0 && block[i + 3] == tag)
                return i;
        }

        return -1;
    }
}
