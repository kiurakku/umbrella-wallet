using System.Security.Cryptography;

namespace Umbrella.Wallet.Core.Derivation;

/// <summary>
/// TON wallet <b>v4R2</b> address derivation from an ed25519 public key.
///
/// The TON address is the representation hash of the wallet's StateInit cell (code + data), encoded
/// user-friendly (flag | workchain | hash | CRC16, base64url). The cell hashing and encoding are
/// pinned by a unit test against <c>tonweb</c>'s output, so the address matches the reference TON
/// implementation byte-for-byte — a wrong layout would send funds to an unspendable address.
///
/// Only the receive address is derived here; TON transaction building is not implemented, so TON is
/// exposed as receive-only. The key is the ed25519 key at SLIP-0010 <c>m/44'/607'/0'</c> (the TON
/// coin type), matching multi-coin wallets so the same BIP39 phrase recovers the funds elsewhere.
/// </summary>
public static class TonKeys
{
    // Wallet v4R2 code cell: its representation hash and depth (from the published code / tonweb).
    private static readonly byte[] CodeHash =
        Convert.FromHexString("feb5ff6820e2ff0d9483e7e0d62c817d846789fb4ae580c878866d959dabd5c0");
    private const int CodeDepth = 7;

    /// <summary>The v4 default subwallet id (0x29a9a317).</summary>
    private const uint SubwalletId = 698983191;

    /// <summary>User-friendly v4R2 address for an ed25519 public key. Non-bounceable (UQ) by default,
    /// which is the correct form for a plain receiving wallet.</summary>
    public static string WalletV4R2Address(byte[] publicKey, bool bounceable = false, bool testnet = false)
    {
        if (publicKey is null || publicKey.Length != 32)
            throw new ArgumentException("A TON public key must be 32 bytes.", nameof(publicKey));

        // data cell: seqno:uint32=0, subwallet_id:uint32, public_key:uint256, plugins:HashmapE=empty.
        // 321 bits, no refs. Last byte holds the empty-dict bit (0) plus the cell augmentation → 0x40.
        var data = new byte[41];
        data[4] = (byte)((SubwalletId >> 24) & 0xFF);
        data[5] = (byte)((SubwalletId >> 16) & 0xFF);
        data[6] = (byte)((SubwalletId >> 8) & 0xFF);
        data[7] = (byte)(SubwalletId & 0xFF);
        Buffer.BlockCopy(publicKey, 0, data, 8, 32);
        data[40] = 0x40;
        var dataHash = CellHash(0x00, 0x51, data, Array.Empty<int>(), Array.Empty<byte[]>());

        // StateInit cell: 5 bits 00110 (code + data present) → augmented byte 0x34; refs [code, data].
        var stateInitHash = CellHash(
            0x02, 0x01, new byte[] { 0x34 },
            new[] { CodeDepth, 0 },
            new[] { CodeHash, dataHash });

        return EncodeAddress(0, stateInitHash, bounceable, testnet);
    }

    /// <summary>Representation hash: SHA-256(d1 | d2 | data | each ref depth as u16 BE | each ref hash).</summary>
    private static byte[] CellHash(byte d1, byte d2, byte[] data, int[] refDepths, byte[][] refHashes)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(d1);
        ms.WriteByte(d2);
        ms.Write(data);
        foreach (var depth in refDepths)
        {
            ms.WriteByte((byte)(depth >> 8));
            ms.WriteByte((byte)depth);
        }

        foreach (var hash in refHashes) ms.Write(hash);
        return SHA256.HashData(ms.ToArray());
    }

    private static string EncodeAddress(int workchain, byte[] hash, bool bounceable, bool testnet)
    {
        var tag = (byte)(bounceable ? 0x11 : 0x51);
        if (testnet) tag |= 0x80;

        var addr = new byte[36];
        addr[0] = tag;
        addr[1] = (byte)workchain;
        Buffer.BlockCopy(hash, 0, addr, 2, 32);
        var crc = Crc16(addr.AsSpan(0, 34));
        addr[34] = (byte)(crc >> 8);
        addr[35] = (byte)crc;

        return Convert.ToBase64String(addr).Replace('+', '-').Replace('/', '_');
    }

    /// <summary>CRC-16/XMODEM (CCITT), polynomial 0x1021, initial value 0 — TON's address checksum.</summary>
    private static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }

        return crc;
    }
}
