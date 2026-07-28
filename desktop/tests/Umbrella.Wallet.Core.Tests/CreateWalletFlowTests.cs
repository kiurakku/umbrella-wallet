using Umbrella.Wallet.Core.Chains;
using Umbrella.Wallet.Core.Derivation;
using Umbrella.Wallet.Core.Seed;
using Umbrella.Wallet.Infrastructure;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// Covers the exact sequence the Create-wallet button runs, after it was reported as
/// "no phrase is generated". The generator and vault were fine; the form swallowed
/// validation errors into the title bar. These tests pin the flow itself.
/// </summary>
public sealed class CreateWalletFlowTests : IDisposable
{
    private const string Password = "a-very-long-vault-password";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"umbrella-create-flow-{Guid.NewGuid():N}");

    [Fact]
    public async Task CreateWallet_ProducesA24WordPhrase_AndPersistsIt()
    {
        var mnemonics = new Bip39MnemonicService();
        var vault = new EncryptedFileSeedVault(Path.Combine(_directory, "vault.json"));

        var phrase = mnemonics.Generate();
        await vault.CreateAsync(phrase, Password);

        Assert.Equal(24, phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.True(mnemonics.Validate(phrase).IsValid);
        Assert.Equal(phrase, await vault.UnlockAsync(Password));
    }

    [Fact]
    public void Generate_ProducesADifferentPhraseEveryTime()
    {
        var mnemonics = new Bip39MnemonicService();
        var phrases = Enumerable.Range(0, 16).Select(_ => mnemonics.Generate()).ToList();

        Assert.Equal(phrases.Count, phrases.Distinct().Count());
    }

    /// <summary>
    /// The vault throws below 12 chars. The form must reject at the same boundary, otherwise
    /// the click dies inside the vault and looks like a dead button.
    /// </summary>
    [Fact]
    public async Task CreateWallet_ShortPassword_IsRejectedByTheVault()
    {
        var vault = new EncryptedFileSeedVault(Path.Combine(_directory, "vault.json"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => vault.CreateAsync(new Bip39MnemonicService().Generate(), "short"));
        Assert.False(vault.Exists);
    }

    [Fact]
    public async Task AfterCreate_EverySupportedChainDerivesARealAddress()
    {
        var vault = new EncryptedFileSeedVault(Path.Combine(_directory, "vault.json"));
        var phrase = new Bip39MnemonicService().Generate();
        await vault.CreateAsync(phrase, Password);

        var mnemonic = await vault.UnlockAsync(Password);
        var deriver = new HdAddressDeriver();

        foreach (var chain in ChainCatalog.Supported)
        {
            var account = deriver.DeriveReceiveAddress(mnemonic, chain.Id);

            Assert.False(string.IsNullOrWhiteSpace(account.Address));
            Assert.DoesNotContain("Unlock", account.Address, StringComparison.Ordinal);
            Assert.DoesNotContain("Adapter", account.Address, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every receive-only chain must produce a REAL, restorable address, never a stub — Monero
    /// (its own 25-word account), TON (wallet v4R2) and Cardano (CIP-1852 base address).
    /// </summary>
    [Fact]
    public async Task ReceiveOnlyChains_ProduceRealAddresses_NotStubs()
    {
        var vault = new EncryptedFileSeedVault(Path.Combine(_directory, "vault.json"));
        var phrase = new Bip39MnemonicService().Generate();
        await vault.CreateAsync(phrase, Password);

        var mnemonic = await vault.UnlockAsync(Password);
        var deriver = new HdAddressDeriver();

        // Nothing is left "planned" — everything derives a real address.
        Assert.Empty(ChainCatalog.Planned);

        // Monero: a real 95-char mainnet address.
        var monero = deriver.DeriveReceiveAddress(mnemonic, ChainId.Xmr);
        Assert.Equal(95, monero.Address.Length);
        Assert.StartsWith("4", monero.Address, StringComparison.Ordinal);

        // TON: a real wallet-v4R2 address (48-char UQ form).
        var ton = deriver.DeriveReceiveAddress(mnemonic, ChainId.Ton);
        Assert.Equal(48, ton.Address.Length);
        Assert.StartsWith("UQ", ton.Address, StringComparison.Ordinal);

        // Cardano: a real Shelley base address (bech32 addr1...).
        var ada = deriver.DeriveReceiveAddress(mnemonic, ChainId.Ada);
        Assert.StartsWith("addr1", ada.Address, StringComparison.Ordinal);
        Assert.DoesNotContain("Adapter", ada.Address, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
