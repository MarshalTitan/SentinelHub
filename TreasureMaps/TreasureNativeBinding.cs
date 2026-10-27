using System.Buffers.Binary;

namespace SentinelHub.TreasureMaps;

// Match the treasure call's rank/spot/mode setup, not a generic UI singleton.
// Caller and callee audited against game 2026.10.16.0000.0000.
public static class TreasureNativeBinding
{
    public const string CallSite = "0F B6 D0 45 33 C9 44 0F B7 C3 48 8B CF E8 ?? ?? ?? ?? 40 84 F6";
    public const int CallSiteLength = 21;
    public const int HandlerLength = 75;
    private static ReadOnlySpan<byte> Arguments => [0x0F, 0xB6, 0xD0, 0x45, 0x33, 0xC9,
        0x44, 0x0F, 0xB7, 0xC3, 0x48, 0x8B, 0xCF, 0xE8];
    private static ReadOnlySpan<byte> AfterCall => [0x40, 0x84, 0xF6];
    private static ReadOnlySpan<byte> HandlerStart => [0x4C, 0x8B, 0xDC, 0x55, 0x53, 0x56, 0x49, 0x8D, 0xAB];
    private static ReadOnlySpan<byte> HandlerArguments => [0x4C, 0x8B, 0xF1, 0x44, 0x0F, 0xB7, 0xFA,
        0x33, 0xC9, 0x41, 0x0F, 0xB6, 0xD9, 0x41, 0x0F, 0xB7, 0xF8];

    public static bool TryGetTarget(ReadOnlySpan<byte> bytes, long site, long textStart, long textEnd, out long target)
    {
        target = 0;
        if (textStart <= 0 || textEnd <= textStart || textEnd - textStart < HandlerLength
            || site < textStart || site > textEnd - CallSiteLength || bytes.Length < CallSiteLength
            || !bytes.StartsWith(Arguments) || !bytes.Slice(18, 3).SequenceEqual(AfterCall)) return false;
        try { target = checked(site + 18 + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(14, 4))); }
        catch (OverflowException) { return false; }
        if (target < textStart || target > textEnd - HandlerLength) { target = 0; return false; }
        return true;
    }

    public static bool IsTreasureHandler(ReadOnlySpan<byte> bytes) => bytes.Length >= HandlerLength
        && bytes.StartsWith(HandlerStart) && bytes.Slice(0x3A, HandlerArguments.Length).SequenceEqual(HandlerArguments);
}
