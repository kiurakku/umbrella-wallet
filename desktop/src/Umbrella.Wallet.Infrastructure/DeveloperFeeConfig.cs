using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbrella.Wallet.Infrastructure;

/// <summary>
/// The developer (platform) fee taken on a send, and the address that receives it — per chain.
///
/// This is deliberately a plain, unencrypted local file (<c>developer-fee.json</c> next to the
/// vault): a receiving address is public, not a secret, and the fee has to be readable while
/// building a transaction. It carries NO private keys.
///
/// <para>
/// Works with no backend: the fee percentage and the receiving addresses live entirely on the
/// device. To ship a fee to every user of a build, set the <see cref="Defaults"/> below before
/// publishing — those become the baked-in values, and the admin panel writes a local override on
/// top. With <see cref="FeeBps"/> at 0 (the default) the wallet behaves exactly as a zero-fee
/// wallet until a developer configures it.
/// </para>
///
/// <para>
/// The fee is ALWAYS disclosed in the send review before the user confirms — see the send flow.
/// A hidden skim is neither built nor supported.
/// </para>
/// </summary>
public sealed class DeveloperFeeConfig
{
    /// <summary>Hard ceiling: a misconfiguration can never quote more than 2%.</summary>
    public const int MaxBps = 200;

    /// <summary>
    /// Chains whose send path actually routes the fee on-chain today. The fee is only ever quoted
    /// and taken for these — so the disclosure can never claim a fee the wallet does not collect.
    /// Others are configurable in the admin panel but marked "routing pending" until wired.
    /// </summary>
    public static readonly IReadOnlySet<string> RoutedChains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BTC", "LTC", "XMR" };

    /// <summary>
    /// Build-time defaults. Edit these before publishing to ship a fee to every install with no
    /// server. Left empty / 0 so an unconfigured build takes nothing.
    /// </summary>
    private static DeveloperFeeConfig Defaults() => new()
    {
        FeeBps = 0,
        Addresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>Fee in basis points (50 = 0.5%). 0 disables the fee entirely.</summary>
    public int FeeBps { get; set; }

    /// <summary>Receiving address per canonical chain symbol (BTC, LTC, ETH, SOL, TRX, USDT, XMR).</summary>
    public Dictionary<string, string> Addresses { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public int EffectiveBps => Math.Clamp(FeeBps, 0, MaxBps);

    [JsonIgnore]
    public decimal FeePercent => EffectiveBps / 100m;

    /// <summary>Whether on-chain routing is implemented for this chain today.</summary>
    public static bool IsRouted(string symbol) => RoutedChains.Contains(Canon(symbol));

    /// <summary>The configured receiving address for a chain, or null if none/blank.</summary>
    public string? AddressFor(string symbol)
    {
        return Addresses.TryGetValue(Canon(symbol), out var a) && !string.IsNullOrWhiteSpace(a)
            ? a.Trim()
            : null;
    }

    /// <summary>Fee amount for a send, in the sent asset's units. Fee is added on top of the amount.</summary>
    public decimal FeeAmount(decimal amount) => amount <= 0 ? 0m : amount * EffectiveBps / 10_000m;

    /// <summary>
    /// The fee to take for this send, or null if none applies. Returns a value only when the fee
    /// is enabled, a receiving address is configured, AND the chain's send path routes it today.
    /// </summary>
    public (decimal Amount, string Address)? QuoteFee(string symbol, decimal amount)
    {
        if (EffectiveBps <= 0 || amount <= 0 || !IsRouted(symbol)) return null;
        var address = AddressFor(symbol);
        if (address is null) return null;
        var fee = FeeAmount(amount);
        return fee <= 0 ? null : (fee, address);
    }

    // --- Persistence ------------------------------------------------------

    private static string Path => System.IO.Path.Combine(AppPaths.DataRoot, "developer-fee.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static DeveloperFeeConfig Load()
    {
        try
        {
            if (!File.Exists(Path)) return Defaults();
            var loaded = JsonSerializer.Deserialize<DeveloperFeeConfig>(File.ReadAllText(Path));
            if (loaded is null) return Defaults();
            loaded.Addresses = new Dictionary<string, string>(loaded.Addresses, StringComparer.OrdinalIgnoreCase);
            return loaded;
        }
        catch
        {
            return Defaults();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // A read-only install directory must not crash the app; the fee just stays unset.
        }
    }

    /// <summary>Canonical chain symbol so TRC-20/TRON variants collapse to one key.</summary>
    private static string Canon(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();
        return s switch
        {
            "TRON" or "TRC20" => "TRX",
            "USDT-TRC20" or "USDT_TRC20" => "USDT",
            "MONERO" => "XMR",
            _ => s,
        };
    }
}
