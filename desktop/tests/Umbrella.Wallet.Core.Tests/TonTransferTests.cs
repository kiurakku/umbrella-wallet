using System.Numerics;
using Umbrella.Wallet.Core.Derivation;
using Umbrella.Wallet.Core.Ton;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// Pins TON wallet v4R2 transfer construction to the reference <c>@ton/ton</c> library. The expected
/// cell hashes and signatures were produced by @ton for a fixed ed25519 seed, destination, amount and
/// valid-until. Cell hashes are canonical (independent of BoC byte layout), so matching the signing
/// message hash — and the deterministic ed25519 signature over it — proves the transaction the network
/// executes is byte-for-byte what @ton would send. Any drift changes a hash and fails here, before a
/// single nanoton can move.
/// </summary>
public sealed class TonTransferTests
{
    private static readonly byte[] Seed =
        Convert.FromHexString("0101020305080d1522375990e97962dbbd18d5f2278916a301040900190a2b45");

    private const string PublicKeyHex = "10d3e0432d6025017279bf69da4a35bb7a2b06b90abd66e7c8cf51e989d4e993";
    private const string FromAddress = "UQD68-kPl2qXkKCBZUCwo1OS6Lih0vdMkNGa3irlr2WC8HhN";
    private const string DestAddress = "UQAUGur5EsUnRjaGADHOuNpsaTgto82lGpzbfyLm11ymwHON";
    private static readonly BigInteger AmountNano = 1234500000;
    private const uint ValidUntil = 0xffffffff;

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    [Fact]
    public void Public_key_and_address_match_reference()
    {
        Assert.Equal(PublicKeyHex, Hex(TonTransfer.PublicKey(Seed)));
        Assert.Equal(FromAddress, TonKeys.WalletV4R2Address(TonTransfer.PublicKey(Seed), bounceable: false));
    }

    [Fact]
    public void V4r2_code_cell_matches_hash_and_round_trips()
    {
        Assert.Equal("feb5ff6820e2ff0d9483e7e0d62c817d846789fb4ae580c878866d959dabd5c0", TonTransfer.CodeCell().HashHex());
        // The BoC serialiser/deserialiser round-trips the code tree without changing its hash.
        Assert.Equal(TonTransfer.CodeCell().HashHex(), TonCell.FromBoc(TonTransfer.CodeCell().ToBoc()).HashHex());
    }

    [Fact]
    public void State_init_hash_is_the_account_address()
    {
        var (_, addrHash, _) = TonTransfer.ParseFriendlyAddress(FromAddress);
        Assert.Equal(Hex(addrHash), TonTransfer.StateInit(TonTransfer.PublicKey(Seed)).HashHex());
    }

    [Fact]
    public void ParseFriendlyAddress_rejects_a_mistyped_address_via_the_checksum()
    {
        // Decode a real address, corrupt one hash byte but keep the original CRC-16 trailer:
        // this is exactly what a typo produces — a 36-byte blob with a valid tag but a stale checksum.
        // Without checksum verification the wallet would happily send funds to this wrong hash.
        var s = DestAddress.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        var raw = Convert.FromBase64String(s);
        raw[10] ^= 0x01; // flip a bit inside the 32-byte hash; leave bytes 34-35 (the CRC) intact
        var corrupted = Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_');

        var ex = Assert.Throws<ArgumentException>(() => TonTransfer.ParseFriendlyAddress(corrupted));
        Assert.Contains("checksum", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The untouched address still parses — the guard rejects corruption, not valid addresses.
        var (wc, _, _) = TonTransfer.ParseFriendlyAddress(DestAddress);
        Assert.Equal(0, wc);
    }

    private static TonCell Order(string? comment)
    {
        var (wc, hash, _) = TonTransfer.ParseFriendlyAddress(DestAddress);
        return TonTransfer.BuildInternalMessage(wc, hash, AmountNano, bounce: false, comment);
    }

    private void CheckScenario(uint seqno, string? comment, string orderHash, string signingHash, string signatureHex)
    {
        var order = Order(comment);
        Assert.Equal(orderHash, order.HashHex());

        var signing = TonTransfer.BuildSigningMessage(
            ValidUntil, seqno, new[] { (TonTransfer.ModePayFeesSeparately, order) });
        Assert.Equal(signingHash, signing.HashHex());

        Assert.Equal(signatureHex, Hex(TonTransfer.Sign(Seed, signing.Hash())));
    }

    [Fact]
    public void Deploy_send_seqno0_matches_reference() => CheckScenario(
        0, null,
        "d8a69ba61bd1e7e2814e0eba5d7c70aefacdb3e7c1bf9a0b40ca90b311d0a097",
        "c83300e7bbf15f2a5267d043626b15272c1507638ff90504f56ce0ce6aff9fda",
        "fc648788f82bd6b1cb444e33c94aeaac02f7ca7702697c6669f9c6c013e8c16db08dcaa727772f57e5af2fb3045263946a5bd4be69a3f5abc3955c0e761e820c");

    [Fact]
    public void Later_send_seqno5_matches_reference() => CheckScenario(
        5, null,
        "d8a69ba61bd1e7e2814e0eba5d7c70aefacdb3e7c1bf9a0b40ca90b311d0a097",
        "f3f14d0e65b5d6b1b6b45381f2eba70f9ff341f5cda4e9e789846270a59997ed",
        "1dfeb951028170be1e6ee34cc54181597666279d212178b32660ca91525dd273cc9fca77c23e2dacdcbcaa00ac0e577f2488defb2f3b90197170001a4d131906");

    [Fact]
    public void Send_with_comment_matches_reference() => CheckScenario(
        5, "gm from umbra",
        "2bef4fbee5244af58df40ae93808c1f6f98646ec55cf814573dc41253e4fe258",
        "95daaebeec71642e645bf01ea495a28582d0eb3f7ecac05cca64888550bcd132",
        "ac0862ebf63d7034910003b668d152dcded351333a54d37f567f2fff26314f5c0e08254632ff8674e2316f85a4af8dc49c958e40940f261a4a0e92f726b72605");

    [Fact]
    public void Full_external_message_is_valid_boc()
    {
        // End-to-end: the produced BoC must deserialise back to a cell (well-formed) for both the
        // deploy path (with StateInit) and a later send.
        var (fromWc, fromHash, _) = TonTransfer.ParseFriendlyAddress(FromAddress);
        var pub = TonTransfer.PublicKey(Seed);
        foreach (var seqno in new uint[] { 0, 5 })
        {
            var boc = TonTransfer.BuildSignedTransferBoc(
                Seed, pub, fromWc, fromHash, seqno, ValidUntil, DestAddress, AmountNano,
                comment: null, sendMode: TonTransfer.ModePayFeesSeparately);
            var root = TonCell.FromBoc(Convert.FromBase64String(boc));
            Assert.True(root.Bits.Length > 0);
        }
    }
}
