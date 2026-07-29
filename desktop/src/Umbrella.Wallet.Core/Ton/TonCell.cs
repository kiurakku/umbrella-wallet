using System.Numerics;
using System.Security.Cryptography;

namespace Umbrella.Wallet.Core.Ton;

/// <summary>
/// A minimal TON cell — up to 1023 data bits and up to 4 references — with the representation hash,
/// depth, and Bag-of-Cells serialisation/deserialisation the wallet needs to build transfer messages.
///
/// This is the fund-critical core of TON sending. Every piece is pinned in <c>TonCellTests</c> against
/// vectors produced by the reference <c>@ton/ton</c> library: the cell hashes are canonical (they do
/// not depend on serialisation order), so if our hash of the signing message matches the reference,
/// the deterministic ed25519 signature over it is byte-identical and the network executes exactly the
/// transaction the reference library would have produced. A wrong bit changes the hash and the test
/// fails long before any funds move.
/// </summary>
public sealed class TonCell
{
    public bool[] Bits { get; }
    public TonCell[] Refs { get; }

    public TonCell(bool[] bits, TonCell[] refs)
    {
        if (bits.Length > 1023) throw new ArgumentException("A cell holds at most 1023 bits.");
        if (refs.Length > 4) throw new ArgumentException("A cell holds at most 4 references.");
        Bits = bits;
        Refs = refs;
    }

    /// <summary>d2 = 2·⌊bits/8⌋ + (partial byte ? 1 : 0), matching the TON cell descriptor.</summary>
    private int D2 => (Bits.Length / 8 * 2) + (Bits.Length % 8 == 0 ? 0 : 1);

    private int D1 => Refs.Length; // ordinary cell, level 0, not exotic

    /// <summary>The data bytes with the TON "augmentation" bit: for a non-byte-aligned cell a single
    /// 1 bit is appended after the data and the byte is zero-filled, so the length is recoverable.</summary>
    public byte[] DataBytes()
    {
        var full = Bits.Length % 8 == 0;
        var byteLen = (Bits.Length + 7) / 8;
        var bytes = new byte[byteLen];
        for (var i = 0; i < Bits.Length; i++)
            if (Bits[i]) bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        if (!full) bytes[Bits.Length / 8] |= (byte)(1 << (7 - (Bits.Length % 8))); // augmentation 1
        return bytes;
    }

    public int Depth()
    {
        if (Refs.Length == 0) return 0;
        var max = 0;
        foreach (var r in Refs) max = Math.Max(max, r.Depth());
        return max + 1;
    }

    /// <summary>Representation hash: SHA-256(d1 | d2 | data | each ref depth u16-BE | each ref hash).</summary>
    public byte[] Hash()
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)D1);
        ms.WriteByte((byte)D2);
        ms.Write(DataBytes());
        foreach (var r in Refs)
        {
            var d = r.Depth();
            ms.WriteByte((byte)(d >> 8));
            ms.WriteByte((byte)d);
        }

        foreach (var r in Refs) ms.Write(r.Hash());
        return SHA256.HashData(ms.ToArray());
    }

    public string HashHex() => Convert.ToHexString(Hash()).ToLowerInvariant();

    // ---- Bag of Cells (serialise) -------------------------------------------------------------

    /// <summary>Serialises this cell (as the single root) into a standard BoC with a CRC32-C trailer.
    /// Our own byte layout need not match the reference library's — the network accepts any BoC that
    /// deserialises to the same cell tree — but the cell tree (and thus every hash) is identical.</summary>
    public byte[] ToBoc()
    {
        var order = TopologicalOrder();
        var index = new Dictionary<string, int>();
        for (var i = 0; i < order.Count; i++) index[order[i].HashHex()] = i;

        var refSize = BytesFor(order.Count);

        // Each cell: d1 | d2 | data | refCount × refIndex(refSize).
        var cellBlobs = new List<byte[]>();
        var totalSize = 0;
        foreach (var c in order)
        {
            using var cs = new MemoryStream();
            cs.WriteByte((byte)c.D1);
            cs.WriteByte((byte)c.D2);
            cs.Write(c.DataBytes());
            foreach (var r in c.Refs)
                WriteBe(cs, index[r.HashHex()], refSize);
            var blob = cs.ToArray();
            cellBlobs.Add(blob);
            totalSize += blob.Length;
        }

        var offBytes = BytesFor(totalSize);
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0xB5, 0xEE, 0x9C, 0x72 });        // magic
        ms.WriteByte((byte)(0x40 | refSize));                    // has_crc32c=1, size=refSize
        ms.WriteByte((byte)offBytes);
        WriteBe(ms, order.Count, refSize);                       // cells
        WriteBe(ms, 1, refSize);                                 // roots
        WriteBe(ms, 0, refSize);                                 // absent
        WriteBe(ms, totalSize, offBytes);                        // total cell-data size
        WriteBe(ms, 0, refSize);                                 // root index 0
        foreach (var blob in cellBlobs) ms.Write(blob);

        var body = ms.ToArray();
        var crc = Crc32C.Compute(body);
        var outp = new byte[body.Length + 4];
        Buffer.BlockCopy(body, 0, outp, 0, body.Length);
        BitConverter.GetBytes(crc).CopyTo(outp, body.Length);    // CRC32-C, little-endian
        return outp;
    }

    /// <summary>Cells ordered so every reference points to a strictly later index (parents first),
    /// which is what a valid BoC requires.</summary>
    private List<TonCell> TopologicalOrder()
    {
        var post = new List<TonCell>();
        var seen = new HashSet<string>();
        void Visit(TonCell c)
        {
            var h = c.HashHex();
            if (!seen.Add(h)) return;
            foreach (var r in c.Refs) Visit(r);
            post.Add(c); // children before self
        }

        Visit(this);
        post.Reverse();  // self before children → parents get lower indices
        // Dedup preserving first occurrence (a shared child may appear once already).
        var order = new List<TonCell>();
        var placed = new HashSet<string>();
        foreach (var c in post)
            if (placed.Add(c.HashHex())) order.Add(c);
        return order;
    }

    // ---- Bag of Cells (deserialise) -----------------------------------------------------------

    /// <summary>Parses a single-root BoC (as produced by TON tooling) back into a cell tree. Used to
    /// load the wallet v4R2 code and data cells from their known BoC constants for the deploy message.</summary>
    public static TonCell FromBoc(byte[] boc)
    {
        var p = 0;
        if (boc.Length < 5 || boc[0] != 0xB5 || boc[1] != 0xEE || boc[2] != 0x9C || boc[3] != 0x72)
            throw new ArgumentException("Not a standard BoC (bad magic).");
        p = 4;
        var flags = boc[p++];
        var hasCrc = (flags & 0x40) != 0;
        var refSize = flags & 0x07;
        var offBytes = boc[p++];
        var cells = ReadBe(boc, ref p, refSize);
        var roots = ReadBe(boc, ref p, refSize);
        ReadBe(boc, ref p, refSize);              // absent
        ReadBe(boc, ref p, offBytes);             // total size
        var rootIndex = 0;
        for (var i = 0; i < roots; i++) rootIndex = ReadBe(boc, ref p, refSize);

        // First pass: read raw descriptors (bits + ref indices).
        var rawBits = new bool[cells][];
        var rawRefs = new int[cells][];
        for (var i = 0; i < cells; i++)
        {
            var d1 = boc[p++];
            var d2 = boc[p++];
            var refCount = d1 & 7;
            var dataLen = (d2 >> 1) + (d2 & 1);
            var data = new byte[dataLen];
            Array.Copy(boc, p, data, 0, dataLen);
            p += dataLen;
            rawBits[i] = UnpackBits(data, (d2 & 1) == 1);
            var refs = new int[refCount];
            for (var r = 0; r < refCount; r++) refs[r] = ReadBe(boc, ref p, refSize);
            rawRefs[i] = refs;
        }

        if (hasCrc) p += 4; // trailer not re-validated on load

        // Second pass: build cells from the highest index down (refs always point to a later index).
        var built = new TonCell[cells];
        for (var i = cells - 1; i >= 0; i--)
        {
            var refs = new TonCell[rawRefs[i].Length];
            for (var r = 0; r < refs.Length; r++) refs[r] = built[rawRefs[i][r]];
            built[i] = new TonCell(rawBits[i], refs);
        }

        return built[rootIndex];
    }

    private static bool[] UnpackBits(byte[] data, bool augmented)
    {
        var totalBits = data.Length * 8;
        if (augmented && data.Length > 0)
        {
            // Real length ends at the augmentation 1 bit: strip it and the trailing zeros.
            var last = data[^1];
            var trailingZeros = 0;
            while (trailingZeros < 8 && (last & (1 << trailingZeros)) == 0) trailingZeros++;
            totalBits -= trailingZeros + 1;
        }

        var bits = new bool[totalBits];
        for (var i = 0; i < totalBits; i++)
            bits[i] = (data[i / 8] & (1 << (7 - (i % 8)))) != 0;
        return bits;
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static int BytesFor(int value)
    {
        var n = 1;
        while ((1L << (8 * n)) <= value) n++;
        return n;
    }

    private static void WriteBe(Stream s, int value, int bytes)
    {
        for (var i = bytes - 1; i >= 0; i--) s.WriteByte((byte)(value >> (8 * i)));
    }

    private static int ReadBe(byte[] data, ref int p, int bytes)
    {
        var v = 0;
        for (var i = 0; i < bytes; i++) v = (v << 8) | data[p++];
        return v;
    }
}
