using Umbrella.Wallet.Core.Cardano;
using Umbrella.Wallet.Core.Derivation;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// Pins Cardano payment-transaction building and BIP32-Ed25519 signing to reference vectors produced
/// by Emurgo's cardano-serialization-lib for the fixed test key (all-zero BIP39 entropy) and a fixed
/// transaction: two outputs, a fee and a TTL. The body CBOR, its blake2b-256 hash, the signature and
/// the fully-assembled signed transaction all match byte-for-byte. Because the signature covers the
/// body hash and the body hash covers the CBOR, matching the signed tx proves the network would
/// execute exactly the transfer the reference library builds — any wrong byte fails here first.
/// </summary>
public sealed class AdaTransferTests
{
    private const string Mnemonic = Bip39MnemonicServiceTests.FixedTwentyFourWordMnemonic;
    private const uint H = 0x80000000;

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    private static byte[] PaymentKey()
    {
        var master = AdaKeys.MasterKey(AdaKeys.EntropyFromMnemonic(Mnemonic));
        var account = AdaKeys.Derive(AdaKeys.Derive(AdaKeys.Derive(master, 1852 | H), 1815 | H), 0 | H);
        return AdaKeys.Derive(AdaKeys.Derive(account, 0), 0); // m/1852'/1815'/0'/0/0
    }

    private static (byte[] Body, byte[] Payment) FixedTx()
    {
        var self = AdaTransfer.DecodeAddress(AdaKeys.BaseAddress(Mnemonic));
        var inputs = new[] { new AdaTransfer.TxInput(Enumerable.Repeat((byte)0x11, 32).ToArray(), 0) };
        var outputs = new[]
        {
            new AdaTransfer.TxOutput(self, 3_000_000),
            new AdaTransfer.TxOutput(self, 6_800_000),
        };
        var body = AdaTransfer.BuildBody(inputs, outputs, fee: 200_000, ttl: 100_000_000);
        return (body, PaymentKey());
    }

    [Fact]
    public void Decoded_self_address_round_trips_from_bech32()
    {
        var raw = AdaTransfer.DecodeAddress(AdaKeys.BaseAddress(Mnemonic));
        Assert.Equal(57, raw.Length);      // header (1) + payment hash (28) + stake hash (28)
        Assert.Equal(0x01, raw[0]);        // mainnet base-address header
    }

    [Fact]
    public void Body_cbor_matches_cardano_serialization_lib()
    {
        var (body, _) = FixedTx();
        Assert.Equal(
            "a4008182582011111111111111111111111111111111111111111111111111111111111111110001" +
            "828258390100b7847c89d5721592fc0cc8932f50a8f8258b39b93861140a1b99fbc2f45a16a668561" +
            "6e566c00fc081fe59f8bd7ab679ee15e9ce2034461a002dc6c08258390100b7847c89d5721592fc0c" +
            "c8932f50a8f8258b39b93861140a1b99fbc2f45a16a6685616e566c00fc081fe59f8bd7ab679ee15e" +
            "9ce2034461a0067c280021a00030d40031a05f5e100",
            Hex(body));
    }

    [Fact]
    public void Body_hash_matches_reference()
    {
        var (body, _) = FixedTx();
        Assert.Equal("af7c439e1d746098c6eeed8fefa78a625591fe3e2a87644b37fefba3f50632ac",
            Hex(AdaTransfer.HashBody(body)));
    }

    [Fact]
    public void Signature_matches_bip32_ed25519_reference()
    {
        var (body, payment) = FixedTx();
        var sig = AdaTransfer.Sign(payment, AdaTransfer.HashBody(body));
        Assert.Equal(
            "ea0c2fbb4e085594a8077245d01299de91fde6b42b87a6b8e0fae2ccac1e742e" +
            "dfae8463ba1f49737b3517bb8ca1c5ad614a63ee272ca0fbad8235bf7da69d08",
            Hex(sig));
    }

    [Fact]
    public void Signed_transaction_matches_reference_byte_for_byte()
    {
        var (body, payment) = FixedTx();
        var hash = AdaTransfer.HashBody(body);
        var signed = AdaTransfer.BuildSignedTx(body, AdaKeys.PublicKey(payment), AdaTransfer.Sign(payment, hash));
        Assert.Equal(
            "84a4008182582011111111111111111111111111111111111111111111111111111111111111110001" +
            "828258390100b7847c89d5721592fc0cc8932f50a8f8258b39b93861140a1b99fbc2f45a16a668561" +
            "6e566c00fc081fe59f8bd7ab679ee15e9ce2034461a002dc6c08258390100b7847c89d5721592fc0c" +
            "c8932f50a8f8258b39b93861140a1b99fbc2f45a16a6685616e566c00fc081fe59f8bd7ab679ee15e" +
            "9ce2034461a0067c280021a00030d40031a05f5e100a1008182582063c5d69570349e4233a0575811" +
            "464f0e8a3fd329abe76e9bdc3d3f1b959821795840ea0c2fbb4e085594a8077245d01299de91fde6b" +
            "42b87a6b8e0fae2ccac1e742edfae8463ba1f49737b3517bb8ca1c5ad614a63ee272ca0fbad8235bf" +
            "7da69d08f5f6",
            Hex(signed));
    }
}
