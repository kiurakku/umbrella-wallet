using Umbrella.Wallet.Core.Derivation;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// Pins Cardano (Icarus / CIP-1852) derivation to the reference produced by Emurgo's
/// cardano-serialization-lib for the fixed test mnemonic. Each stage — entropy, Icarus master key,
/// derived public key, and the final base address — is checked independently, so a bug in the
/// BIP32-Ed25519 math is localised rather than silently shipping a wrong (unspendable) address.
/// </summary>
public sealed class AdaKeysTests
{
    private const string Mnemonic = Bip39MnemonicServiceTests.FixedTwentyFourWordMnemonic;

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    [Fact]
    public void Entropy_of_test_mnemonic_is_all_zero()
    {
        Assert.Equal(new byte[32], AdaKeys.EntropyFromMnemonic(Mnemonic));
    }

    [Fact]
    public void Icarus_master_key_matches_reference()
    {
        var master = AdaKeys.MasterKey(AdaKeys.EntropyFromMnemonic(Mnemonic));
        Assert.Equal(
            "b07ff3e63c17cd2e0504e4bfd52a98c47abde183ccd0738efc385e764fd91d4b" +
            "d7d399eeef3c4df68facb3f11e4a4d45513ea1e2a8018aa35b3c078714cfdced" +
            "ccc42249e17984c44cf380b489f62c57f84089e150245bf49c436d0b9709c58f",
            Hex(master));
    }

    [Fact]
    public void Derived_payment_and_stake_public_keys_match_reference()
    {
        const uint h = 0x80000000;
        var master = AdaKeys.MasterKey(AdaKeys.EntropyFromMnemonic(Mnemonic));
        var account = AdaKeys.Derive(AdaKeys.Derive(AdaKeys.Derive(master, 1852 | h), 1815 | h), 0 | h);

        Assert.Equal(
            "63c5d69570349e4233a0575811464f0e8a3fd329abe76e9bdc3d3f1b95982179",
            Hex(AdaKeys.PublicKey(AdaKeys.Derive(AdaKeys.Derive(account, 0), 0))));
        Assert.Equal(
            "366598ec425ab8140830c4b5f91716d0f7b113fd7013ef3c90487e9dd1535437",
            Hex(AdaKeys.PublicKey(AdaKeys.Derive(AdaKeys.Derive(account, 2), 0))));
    }

    [Fact]
    public void Base_address_matches_cardano_serialization_lib()
    {
        Assert.Equal(
            "addr1qyqt0pru382hy9vjlsxv3ye02z50sfvt8xunscg5pgden77z73dpdfng2ctw2ekqplqgrljelz7h4dneac27nn3qx3rqrhqvwd",
            AdaKeys.BaseAddress(Mnemonic));
    }
}
