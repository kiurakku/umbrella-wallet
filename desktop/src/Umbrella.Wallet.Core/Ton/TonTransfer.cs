using System.Numerics;
using System.Text;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace Umbrella.Wallet.Core.Ton;

/// <summary>
/// Builds a signed wallet v4R2 external message for a TON transfer, ready to broadcast.
///
/// Correctness is anchored on canonical cell hashes: <c>TonTransferTests</c> checks that the internal
/// (order) cell and the signing-message cell hash to the exact values the reference <c>@ton/ton</c>
/// library produces, and that the ed25519 signature over that hash matches byte-for-byte. Because the
/// signature covers the signing-message hash, matching it proves the transaction the network executes
/// is identical to the reference one — a single wrong bit changes the hash and fails the test.
/// </summary>
public static class TonTransfer
{
    public const uint SubwalletId = 698983191;

    /// <summary>Standard send mode: pay the forward fees separately from the transferred value.</summary>
    public const byte ModePayFeesSeparately = 1;

    // Wallet v4R2 code cell, as a BoC. Guarded at load by the known representation hash, so a bad copy
    // fails safely (send refused) instead of ever producing a wrong deploy.
    private const string CodeBocBase64 =
        "te6cckECFAEAAtQAART/APSkE/S88sgLAQIBIAIPAgFIAwYC5tAB0NMDIXGwkl8E4CLXScEgkl8E4ALTHyGCEHBsdWe9IoIQZHN0cr2wkl8F4AP6QDAg+kQByMoHy//J0O1E0IEBQNch9AQwXIEBCPQKb6Exs5JfB+AF0z/IJYIQcGx1Z7qSODDjDQOCEGRzdHK6kl8G4w0EBQB4AfoA9AQw+CdvIjBQCqEhvvLgUIIQcGx1Z4MesXCAGFAEywUmzxZY+gIZ9ADLaRfLH1Jgyz8gyYBA+wAGAIpQBIEBCPRZMO1E0IEBQNcgyAHPFvQAye1UAXKwjiOCEGRzdHKDHrFwgBhQBcsFUAPPFiP6AhPLassfyz/JgED7AJJfA+ICASAHDgIBIAgNAgFYCQoAPbKd+1E0IEBQNch9AQwAsjKB8v/ydABgQEI9ApvoTGACASALDAAZrc52omhAIGuQ64X/wAAZrx32omhAEGuQ64WPwAARuMl+1E0NcLH4AFm9JCtvaiaECAoGuQ+gIYRw1AgIR6STfSmRDOaQPp/5g3gSgBt4EBSJhxWfMYQE+PKDCNcYINMf0x/THwL4I7vyZO1E0NMf0x/T//QE0VFDuvKhUVG68qIF+QFUEGT5EPKj+AAkpMjLH1JAyx9SMMv/UhD0AMntVPgPAdMHIcAAn2xRkyDXSpbTB9QC+wDoMOAhwAHjACHAAuMAAcADkTDjDQOkyMsfEssfy/8QERITAG7SB/oA1NQi+QAFyMoHFcv/ydB3dIAYyMsFywIizxZQBfoCFMtrEszMyXP7AMhAFIEBCPRR8qcCAHCBAQjXGPoA0z/IVCBHgQEI9FHyp4IQbm90ZXB0gBjIywXLAlAGzxZQBPoCFMtqEssfyz/Jc/sAAgBsgQEI1xj6ANM/MFIkgQEI9Fnyp4IQZHN0cnB0gBjIywXLAlAFzxZQA/oCE8tqyx8Syz/Jc/sAAAr0AMntVAj45Sg=";

    /// <summary>The published wallet v4R2 code hash — the address code hash and the deploy-cell guard.</summary>
    private const string CodeHashHex = "feb5ff6820e2ff0d9483e7e0d62c817d846789fb4ae580c878866d959dabd5c0";

    private static TonCell? _codeCell;

    /// <summary>The v4R2 code cell, decoded once and verified against its known hash.</summary>
    public static TonCell CodeCell()
    {
        if (_codeCell is not null) return _codeCell;
        var cell = TonCell.FromBoc(Convert.FromBase64String(CodeBocBase64));
        if (cell.HashHex() != CodeHashHex)
            throw new InvalidOperationException("Embedded TON v4R2 code failed its hash check — refusing to build a deploy.");
        _codeCell = cell;
        return cell;
    }

    /// <summary>The wallet's initial data cell: seqno 0, subwallet id, public key, empty plugin dict.</summary>
    public static TonCell DataCell(byte[] publicKey) => new TonCellBuilder()
        .StoreUInt(0, 32)
        .StoreUInt(SubwalletId, 32)
        .StoreBytes(publicKey)
        .StoreBit(false)
        .EndCell();

    /// <summary>StateInit for the wallet (code + data), whose hash is the account's address hash.</summary>
    public static TonCell StateInit(byte[] publicKey) => new TonCellBuilder()
        .StoreBit(false)   // split_depth: none
        .StoreBit(false)   // special: none
        .StoreBit(true)    // code: present
        .StoreBit(true)    // data: present
        .StoreBit(false)   // library: empty
        .StoreRef(CodeCell())
        .StoreRef(DataCell(publicKey))
        .EndCell();

    /// <summary>The internal (transfer) message — CommonMsgInfoRelaxed with an optional text comment.</summary>
    public static TonCell BuildInternalMessage(
        int destWorkchain, byte[] destHash, BigInteger amountNano, bool bounce, string? comment)
    {
        var b = new TonCellBuilder()
            .StoreBit(false)                 // int_msg_info$0
            .StoreBit(true)                  // ihr_disabled
            .StoreBit(bounce)                // bounce
            .StoreBit(false)                 // bounced
            .StoreBit(false).StoreBit(false) // src: addr_none
            .StoreAddressStd(destWorkchain, destHash)
            .StoreCoins(amountNano)          // value.grams
            .StoreBit(false)                 // value.other: empty
            .StoreCoins(0)                   // ihr_fee
            .StoreCoins(0)                   // fwd_fee
            .StoreUInt(0, 64)                // created_lt
            .StoreUInt(0, 32)                // created_at
            .StoreBit(false);                // init: none

        if (string.IsNullOrEmpty(comment))
        {
            b.StoreBit(false);               // body inline, empty
        }
        else
        {
            b.StoreBit(false);               // body inline
            b.StoreUInt(0, 32);              // text-comment opcode 0
            b.StoreBytes(Encoding.UTF8.GetBytes(comment));
        }

        return b.EndCell();
    }

    /// <summary>The v4 signing message: subwallet, valid-until, seqno, op 0, then each (mode, ^order).</summary>
    public static TonCell BuildSigningMessage(
        uint validUntil, uint seqno, IReadOnlyList<(byte Mode, TonCell Order)> messages)
    {
        var b = new TonCellBuilder()
            .StoreUInt(SubwalletId, 32)
            .StoreUInt(validUntil, 32)
            .StoreUInt(seqno, 32)
            .StoreUInt(0, 8); // op
        foreach (var (mode, order) in messages)
            b.StoreUInt(mode, 8).StoreRef(order);
        return b.EndCell();
    }

    /// <summary>Deterministic ed25519 signature of a 32-byte cell hash with a 32-byte private seed.</summary>
    public static byte[] Sign(byte[] seed32, byte[] hash32)
    {
        var sig = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(seed32, 0, hash32, 0, hash32.Length, sig, 0);
        return sig;
    }

    public static byte[] PublicKey(byte[] seed32)
    {
        var pub = new byte[Ed25519.PublicKeySize];
        Ed25519.GeneratePublicKey(seed32, 0, pub, 0);
        return pub;
    }

    /// <summary>Signed body cell = signature ‖ signing-message (its bits then its refs).</summary>
    public static TonCell BuildSignedBody(byte[] signature, TonCell signingMessage) =>
        new TonCellBuilder()
            .StoreBytes(signature)
            .StoreBits(signingMessage.Bits)
            .StoreRef(signingMessage.Refs.Length > 0 ? signingMessage.Refs[0] : throw new InvalidOperationException("Signing message must reference at least one order."))
            .EndCell();

    /// <summary>The external-in message wrapping the signed body, with StateInit on the first send.</summary>
    public static TonCell BuildExternalMessage(
        int fromWorkchain, byte[] fromHash, TonCell? stateInit, TonCell signedBody)
    {
        var b = new TonCellBuilder()
            .StoreBit(true).StoreBit(false)  // ext_in_msg_info$10
            .StoreBit(false).StoreBit(false) // src: addr_none
            .StoreAddressStd(fromWorkchain, fromHash)
            .StoreCoins(0);                  // import_fee

        if (stateInit is not null)
            b.StoreBit(true).StoreBit(true).StoreRef(stateInit); // init present, stored by reference
        else
            b.StoreBit(false);

        b.StoreBit(true).StoreRef(signedBody);                   // body stored by reference
        return b.EndCell();
    }

    /// <summary>Full flow: build, sign, and serialise the external message to a base64 BoC to broadcast.</summary>
    public static string BuildSignedTransferBoc(
        byte[] seed32, byte[] publicKey, int fromWorkchain, byte[] fromHash,
        uint seqno, uint validUntil, string destFriendly, BigInteger amountNano,
        string? comment, byte sendMode, bool? bounceOverride = null)
    {
        var (destWc, destHash, destBounceable) = ParseFriendlyAddress(destFriendly);
        var bounce = bounceOverride ?? destBounceable;
        var order = BuildInternalMessage(destWc, destHash, amountNano, bounce, comment);
        var signing = BuildSigningMessage(validUntil, seqno, new[] { (sendMode, order) });
        var signature = Sign(seed32, signing.Hash());
        var signedBody = BuildSignedBody(signature, signing);
        var stateInit = seqno == 0 ? StateInit(publicKey) : null;
        var ext = BuildExternalMessage(fromWorkchain, fromHash, stateInit, signedBody);
        return Convert.ToBase64String(ext.ToBoc());
    }

    /// <summary>Decodes a user-friendly address (UQ/EQ base64url) into workchain, hash, and bounce flag.</summary>
    public static (int Workchain, byte[] Hash, bool Bounceable) ParseFriendlyAddress(string address)
    {
        var s = address.Trim().Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        var raw = Convert.FromBase64String(s);
        if (raw.Length != 36) throw new ArgumentException("A TON address decodes to 36 bytes.");
        var tag = (byte)(raw[0] & 0x7F);            // strip the test-only flag
        if (tag != 0x11 && tag != 0x51) throw new ArgumentException("Unknown TON address tag.");
        var workchain = (sbyte)raw[1];
        var hash = new byte[32];
        Buffer.BlockCopy(raw, 2, hash, 0, 32);
        return (workchain, hash, tag == 0x11);
    }
}
