using Umbrella.Wallet.Infrastructure;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// The delete-wallet flow must remove every piece of wallet data but never the bundled runtime
/// binaries. Run against a temp folder (WipeAll takes the root) so it can't touch a real vault.
/// </summary>
public sealed class DataWiperTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "umbrella-wipe-" + Guid.NewGuid().ToString("N"));

    public DataWiperTests()
    {
        Directory.CreateDirectory(_root);
        // Wallet data that MUST go.
        File.WriteAllText(Path.Combine(_root, "vault.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "watch-addresses.json"), "[]");
        File.WriteAllText(Path.Combine(_root, "exchanges.bin"), "x");
        File.WriteAllText(Path.Combine(_root, "ui-settings.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "vault.json.replaced-20260101000000"), "{}");
        Directory.CreateDirectory(Path.Combine(_root, "profile"));
        File.WriteAllText(Path.Combine(_root, "profile", "avatar.png"), "img");
        Directory.CreateDirectory(Path.Combine(_root, "monero"));
        File.WriteAllText(Path.Combine(_root, "monero", "wallet.keys"), "keys");
        // Runtime binaries that MUST stay.
        Directory.CreateDirectory(Path.Combine(_root, "tor"));
        File.WriteAllText(Path.Combine(_root, "tor", "tor.exe"), "binary");
    }

    [Fact]
    public void WipeAll_removes_every_wallet_artifact()
    {
        var result = DataWiper.WipeAll(_root);

        Assert.False(File.Exists(Path.Combine(_root, "vault.json")));
        Assert.False(File.Exists(Path.Combine(_root, "watch-addresses.json")));
        Assert.False(File.Exists(Path.Combine(_root, "exchanges.bin")));
        Assert.False(File.Exists(Path.Combine(_root, "ui-settings.json")));
        Assert.False(File.Exists(Path.Combine(_root, "vault.json.replaced-20260101000000")));
        Assert.False(Directory.Exists(Path.Combine(_root, "profile")));
        Assert.False(Directory.Exists(Path.Combine(_root, "monero")));
        Assert.Empty(result.Failed);
        Assert.True(result.Removed >= 7);
    }

    [Fact]
    public void WipeAll_preserves_the_bundled_binaries()
    {
        DataWiper.WipeAll(_root);

        // Tor (and any other runtime the app ships) must survive the wipe.
        Assert.True(Directory.Exists(Path.Combine(_root, "tor")));
        Assert.True(File.Exists(Path.Combine(_root, "tor", "tor.exe")));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }
}
