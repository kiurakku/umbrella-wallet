using Umbrella.Wallet.Infrastructure;

namespace Umbrella.Wallet.Core.Tests;

public sealed class WalletRegistryTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), $"umbrella-registry-tests-{Guid.NewGuid():N}");

    private string IndexPath => Path.Combine(_dir, "wallets.json");
    private string LegacyVaultPath => Path.Combine(_dir, "vault.json");
    private string ManagedVaultPath(string id) => Path.Combine(_dir, "wallets", id + ".vault.json");

    private WalletRegistry NewRegistry() =>
        new(IndexPath, LegacyVaultPath, ManagedVaultPath);

    public WalletRegistryTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void FreshInstall_NoLegacyVault_IsEmpty()
    {
        var reg = NewRegistry();

        Assert.False(reg.HasAnyWallet);
        Assert.Null(reg.Active);
        Assert.Empty(reg.Wallets);
    }

    [Fact]
    public void ExistingLegacyVault_IsAutoRegisteredAsFirstActiveWallet()
    {
        File.WriteAllText(LegacyVaultPath, "{}"); // pretend a v2.8-era vault exists

        var reg = NewRegistry();

        Assert.True(reg.HasAnyWallet);
        Assert.NotNull(reg.Active);
        Assert.True(reg.Active!.IsLegacy);
        Assert.Equal(LegacyVaultPath, reg.VaultPathFor(reg.Active));
    }

    [Fact]
    public void Add_CreatesManagedWallet_WithDistinctVaultPath_DoesNotChangeActive()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();
        var legacyId = reg.Active!.Id;

        var added = reg.Add("Savings");

        Assert.False(added.IsLegacy);
        Assert.Equal("Savings", added.Label);
        Assert.NotEqual(LegacyVaultPath, reg.VaultPathFor(added));
        Assert.Equal(legacyId, reg.Active!.Id); // active unchanged by Add
        Assert.Equal(2, reg.Wallets.Count);
    }

    [Fact]
    public void SetActive_SwitchesSelection_AndSurvivesReload()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();
        var added = reg.Add("Trading");

        reg.SetActive(added.Id);
        Assert.Equal(added.Id, reg.Active!.Id);

        var reloaded = NewRegistry();
        Assert.Equal(added.Id, reloaded.Active!.Id);
        Assert.Equal(2, reloaded.Wallets.Count);
    }

    [Fact]
    public void Rename_PersistsNewLabel()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();
        var added = reg.Add("Old");

        reg.Rename(added.Id, "New name");

        Assert.Equal("New name", NewRegistry().Wallets.Single(w => w.Id == added.Id).Label);
    }

    [Fact]
    public void Remove_ActiveWallet_Throws()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();

        Assert.Throws<InvalidOperationException>(() => reg.Remove(reg.Active!.Id));
    }

    [Fact]
    public void Remove_ManagedWallet_DeletesItsVault_ButLeavesOthers()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();
        var added = reg.Add("Temp");
        var managedPath = reg.VaultPathFor(added);
        Directory.CreateDirectory(Path.GetDirectoryName(managedPath)!);
        File.WriteAllText(managedPath, "{}");

        reg.Remove(added.Id); // legacy is active, so this is allowed

        Assert.False(File.Exists(managedPath));      // its vault file is gone
        Assert.True(File.Exists(LegacyVaultPath));   // the main wallet is untouched
        Assert.Single(reg.Wallets);
    }

    [Fact]
    public void Remove_LegacyWallet_NeverDeletesTheMainVaultFile()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        var reg = NewRegistry();
        var added = reg.Add("Second");
        reg.SetActive(added.Id);            // make the managed one active
        var legacy = reg.Wallets.Single(w => w.IsLegacy);

        reg.Remove(legacy.Id);              // de-register the legacy entry

        Assert.True(File.Exists(LegacyVaultPath)); // the seed file itself is preserved
        Assert.DoesNotContain(reg.Wallets, w => w.IsLegacy);
    }

    [Fact]
    public void CorruptIndex_FallsBackToLegacyVault_NoLockout()
    {
        File.WriteAllText(LegacyVaultPath, "{}");
        File.WriteAllText(IndexPath, "{ this is not valid json ]");

        var reg = NewRegistry();

        Assert.True(reg.HasAnyWallet);
        Assert.True(reg.Active!.IsLegacy);
    }

    [Fact]
    public void FirstWalletVaultPath_IsTheLegacyLocation()
    {
        Assert.Equal(LegacyVaultPath, NewRegistry().FirstWalletVaultPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
