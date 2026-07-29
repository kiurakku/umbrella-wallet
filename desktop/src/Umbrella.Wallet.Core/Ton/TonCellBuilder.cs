using System.Numerics;

namespace Umbrella.Wallet.Core.Ton;

/// <summary>Fluent builder for a <see cref="TonCell"/>: appends bits big-endian and collects refs,
/// with the TON-specific encodings the wallet needs (MsgAddressInt, VarUInteger16 "coins").</summary>
public sealed class TonCellBuilder
{
    private readonly List<bool> _bits = new();
    private readonly List<TonCell> _refs = new();

    public TonCellBuilder StoreBit(bool bit)
    {
        _bits.Add(bit);
        return this;
    }

    public TonCellBuilder StoreBits(IEnumerable<bool> bits)
    {
        _bits.AddRange(bits);
        return this;
    }

    /// <summary>Stores the low <paramref name="bits"/> bits of <paramref name="value"/>, big-endian.</summary>
    public TonCellBuilder StoreUInt(ulong value, int bits)
    {
        for (var i = bits - 1; i >= 0; i--) _bits.Add(((value >> i) & 1) == 1);
        return this;
    }

    public TonCellBuilder StoreBytes(byte[] data)
    {
        foreach (var b in data) StoreUInt(b, 8);
        return this;
    }

    public TonCellBuilder StoreRef(TonCell cell)
    {
        _refs.Add(cell);
        return this;
    }

    /// <summary>MsgAddressInt addr_std$10: prefix 10, anycast 0, workchain int8, address uint256.</summary>
    public TonCellBuilder StoreAddressStd(int workchain, byte[] hash256)
    {
        if (hash256.Length != 32) throw new ArgumentException("An address hash is 32 bytes.");
        StoreBit(true);
        StoreBit(false);
        StoreBit(false);                 // anycast: nothing
        StoreUInt((byte)workchain, 8);   // int8, two's complement (0 → 0x00, -1 → 0xFF)
        StoreBytes(hash256);
        return this;
    }

    /// <summary>VarUInteger16 ("Grams"): 4-bit byte length, then that many big-endian value bytes.</summary>
    public TonCellBuilder StoreCoins(BigInteger nano)
    {
        if (nano.Sign < 0) throw new ArgumentException("Coins cannot be negative.");
        if (nano.IsZero) return StoreUInt(0, 4);
        var bytes = nano.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length > 15) throw new ArgumentException("Amount does not fit VarUInteger16.");
        StoreUInt((ulong)bytes.Length, 4);
        StoreBytes(bytes);
        return this;
    }

    public TonCell EndCell() => new(_bits.ToArray(), _refs.ToArray());
}
