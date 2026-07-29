namespace Umbrella.Wallet.Core.Ton;

/// <summary>CRC-32/Castagnoli (reflected, polynomial 0x1EDC6F41 → 0x82F63B78), the checksum TON
/// appends to a Bag of Cells. Table-driven; the result is stored little-endian in the BoC trailer.</summary>
internal static class Crc32C
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0x82F63B78 : crc >> 1;
            table[i] = crc;
        }

        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        return crc ^ 0xFFFFFFFFu;
    }
}
