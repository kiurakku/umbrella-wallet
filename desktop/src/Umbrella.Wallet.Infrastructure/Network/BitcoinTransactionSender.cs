using System.Text;
using System.Text.Json;
using NBitcoin;
using NBitcoin.Altcoins;

namespace Umbrella.Wallet.Infrastructure.Network;

public sealed record UtxoRef(string TxId, int Vout, long ValueSat);

public sealed record BtcSendQuote(
    string Symbol,
    string From,
    string To,
    decimal Amount,
    long AmountSat,
    long FeeSat,
    decimal FeeAmount,
    int InputCount,
    string Explorer,
    string? DevFeeAddress = null,
    long DevFeeSat = 0,
    // Optional OP_RETURN data (<=80 bytes) — carries a THORChain swap memo, without which a swap
    // deposit would be seen as a plain transfer and the funds lost.
    string? Memo = null);

/// <summary>
/// Real BTC / LTC sending over Esplora-style public explorers. UTXOs and fee rates are read
/// from the explorer, the transaction is built and signed LOCALLY with NBitcoin, and only the
/// signed hex is broadcast. Change returns to the same address the wallet displays.
/// </summary>
public sealed class BitcoinTransactionSender
{
    private static HttpClient Http => PublicHttp.Shared;

    private const long DustSat = 546;

    public static string ExplorerFor(string symbol) => symbol.ToUpperInvariant() switch
    {
        "BTC" => "https://blockstream.info/api",
        "LTC" => "https://litecoinspace.org/api",
        _ => throw new NotSupportedException($"No explorer for {symbol}."),
    };

    // NBitcoin.Network must be fully qualified: this file's own namespace is also "Network".
    private static NBitcoin.Network NetworkFor(string symbol) => symbol.ToUpperInvariant() switch
    {
        "BTC" => NBitcoin.Network.Main,
        "LTC" => Litecoin.Instance.Mainnet,
        _ => throw new NotSupportedException($"No network for {symbol}."),
    };

    /// <summary>Validates, gathers UTXOs and the fee rate, and returns a quote. Nothing is signed.</summary>
    /// <param name="devFeeAddress">Optional developer-fee recipient (same chain). Sent as an extra
    /// output in the SAME transaction, so there is only ever one network fee.</param>
    /// <param name="devFeeAmount">The developer fee, added on top of <paramref name="amount"/>.</param>
    public async Task<(BtcSendQuote? Quote, string? Error)> PrepareAsync(
        string symbol,
        string fromAddress,
        string toAddress,
        decimal amount,
        string? devFeeAddress = null,
        decimal devFeeAmount = 0m,
        string? memo = null,
        CancellationToken ct = default)
    {
        // OP_RETURN carries at most 80 bytes on the relay network; refuse rather than build a
        // transaction that nodes will drop (which would look "sent" but never confirm).
        if (!string.IsNullOrEmpty(memo) && System.Text.Encoding.ASCII.GetByteCount(memo) > 80)
            return (null, "Swap memo is too long for an OP_RETURN (max 80 bytes).");

        NBitcoin.Network network;
        string explorer;
        try
        {
            network = NetworkFor(symbol);
            explorer = ExplorerFor(symbol);
        }
        catch (NotSupportedException ex)
        {
            return (null, ex.Message);
        }

        try
        {
            BitcoinAddress.Create(toAddress, network);
        }
        catch
        {
            return (null, $"That is not a valid {symbol.ToUpperInvariant()} address for mainnet.");
        }

        if (amount <= 0) return (null, "Amount must be positive.");
        var amountSat = (long)(amount * 100_000_000m);
        if (amountSat < DustSat) return (null, $"Amount is below the dust limit ({DustSat} sat).");

        // Developer fee output. If the address is unusable or the fee dust, the send simply
        // proceeds without it — a fee misconfiguration must never block the user's transfer, and
        // the quote it returns is the single source of truth the review screen discloses.
        long devFeeSat = 0;
        string? devFee = null;
        if (!string.IsNullOrWhiteSpace(devFeeAddress) && devFeeAmount > 0)
        {
            var candidate = (long)(devFeeAmount * 100_000_000m);
            var valid = false;
            try { BitcoinAddress.Create(devFeeAddress.Trim(), network); valid = true; }
            catch { /* leave the fee off */ }
            if (valid && candidate >= DustSat)
            {
                devFeeSat = candidate;
                devFee = devFeeAddress.Trim();
            }
        }

        var utxos = await FetchUtxosAsync(explorer, fromAddress, ct);
        if (utxos.Count == 0) return (null, "No spendable outputs on this address.");

        var feeRate = await FetchFeeRateAsync(explorer, ct);
        var outputs = devFeeSat > 0 ? 3 : 2; // recipient, (dev fee), change
        // An OP_RETURN adds one more (zero-value) output: ~11 vB overhead plus the memo bytes.
        var opReturnVsize = string.IsNullOrEmpty(memo) ? 0 : System.Text.Encoding.ASCII.GetByteCount(memo) + 11;

        // Select inputs largest-first until the amount plus dev fee plus the (input-count
        // dependent) network fee is met.
        var ordered = utxos.OrderByDescending(u => u.ValueSat).ToList();
        var selected = new List<UtxoRef>();
        long total = 0;
        long fee = 0;
        foreach (var utxo in ordered)
        {
            selected.Add(utxo);
            total += utxo.ValueSat;
            // P2WPKH: ~68 vB per input, 31 vB per output, ~11 vB overhead.
            var vsize = selected.Count * 68 + outputs * 31 + 11 + opReturnVsize;
            fee = (long)Math.Ceiling(vsize * feeRate);
            if (total >= amountSat + devFeeSat + fee) break;
        }

        if (total < amountSat + devFeeSat + fee)
        {
            return (null,
                $"Insufficient funds: have {total / 100_000_000m:0.########} {symbol.ToUpperInvariant()}, " +
                $"need {(amountSat + devFeeSat + fee) / 100_000_000m:0.########} including fee.");
        }

        return (new BtcSendQuote(
            symbol.ToUpperInvariant(), fromAddress, toAddress, amount, amountSat,
            fee, fee / 100_000_000m, selected.Count, explorer, devFee, devFeeSat,
            string.IsNullOrEmpty(memo) ? null : memo), null);
    }

    /// <summary>Builds, signs and broadcasts. The key is supplied by the caller and zeroed there.</summary>
    public async Task<(bool Ok, string? TxId, string? Error)> SignAndBroadcastAsync(
        BtcSendQuote quote,
        Key privateKey,
        CancellationToken ct = default)
    {
        try
        {
            var network = NetworkFor(quote.Symbol);
            var from = privateKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
            if (!string.Equals(from.ToString(), quote.From, StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "Key does not match the sending address — refusing to sign.");
            }

            var needed = quote.AmountSat + quote.DevFeeSat + quote.FeeSat;
            var utxos = await FetchUtxosAsync(quote.Explorer, quote.From, ct);
            var ordered = utxos.OrderByDescending(u => u.ValueSat).ToList();
            var selected = new List<UtxoRef>();
            long total = 0;
            foreach (var utxo in ordered)
            {
                selected.Add(utxo);
                total += utxo.ValueSat;
                if (total >= needed) break;
            }

            if (total < needed)
            {
                return (false, null, "Balance changed since the quote — re-check the transfer.");
            }

            var builder = network.CreateTransactionBuilder();
            foreach (var utxo in selected)
            {
                var coin = new Coin(
                    uint256.Parse(utxo.TxId), (uint)utxo.Vout,
                    Money.Satoshis(utxo.ValueSat), from.ScriptPubKey);
                builder.AddCoins(coin);
            }

            builder.AddKeys(privateKey);
            builder.Send(BitcoinAddress.Create(quote.To, network), Money.Satoshis(quote.AmountSat));
            // Developer fee: an extra output in the same transaction (disclosed before confirm).
            if (quote.DevFeeSat > 0 && !string.IsNullOrWhiteSpace(quote.DevFeeAddress))
            {
                builder.Send(BitcoinAddress.Create(quote.DevFeeAddress, network), Money.Satoshis(quote.DevFeeSat));
            }

            // OP_RETURN swap memo: a zero-value output that tells THORChain what to do with the deposit.
            // Guarded to <=80 bytes at quote time; re-checked here so a hand-built quote can't overflow it.
            if (!string.IsNullOrWhiteSpace(quote.Memo))
            {
                var memoBytes = Encoding.ASCII.GetBytes(quote.Memo);
                if (memoBytes.Length > 80) return (false, null, "Swap memo exceeds the 80-byte OP_RETURN limit.");
                builder.Send(TxNullDataTemplate.Instance.GenerateScriptPubKey(memoBytes), Money.Zero);
            }

            builder.SendFees(Money.Satoshis(quote.FeeSat));
            builder.SetChange(from);

            var tx = builder.BuildTransaction(sign: true);
            if (!builder.Verify(tx, out var errors))
            {
                return (false, null, "Signature verification failed: " + string.Join("; ", errors.Select(e => e.ToString())));
            }

            var hex = tx.ToHex();
            using var content = new StringContent(hex, Encoding.UTF8, "text/plain");
            using var res = await Http.PostAsync($"{quote.Explorer}/tx", content, ct);
            var body = (await res.Content.ReadAsStringAsync(ct)).Trim();
            if (!res.IsSuccessStatusCode)
            {
                return (false, null, $"Explorer rejected the transaction: {body}");
            }

            return (true, body, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Send failed: {ex.Message}");
        }
    }

    private static async Task<List<UtxoRef>> FetchUtxosAsync(string explorer, string address, CancellationToken ct)
    {
        var result = new List<UtxoRef>();
        try
        {
            using var res = await Http.GetAsync($"{explorer}/address/{Uri.EscapeDataString(address)}/utxo", ct);
            if (!res.IsSuccessStatusCode) return result;
            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var txid = item.GetProperty("txid").GetString();
                var vout = item.GetProperty("vout").GetInt32();
                var value = item.GetProperty("value").GetInt64();
                // Only spend confirmed outputs — unconfirmed change can vanish on a reorg.
                var confirmed = !item.TryGetProperty("status", out var status) ||
                                !status.TryGetProperty("confirmed", out var c) || c.GetBoolean();
                if (txid is not null && confirmed) result.Add(new UtxoRef(txid, vout, value));
            }
        }
        catch
        {
            // treated as "no UTXOs"
        }

        return result;
    }

    /// <summary>
    /// Economical sat/vB. Esplora returns a target→rate map; we take the 6-block (~1 hour)
    /// estimate rather than the 3-block one, which is materially cheaper and still confirms
    /// promptly, and fall back through longer targets if the node omits one. Clamped to the
    /// 1 sat/vB relay minimum so a transaction can never be built below the floor.
    /// </summary>
    private static async Task<double> FetchFeeRateAsync(string explorer, CancellationToken ct)
    {
        try
        {
            using var res = await Http.GetAsync($"{explorer}/fee-estimates", ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                foreach (var target in new[] { "6", "10", "12", "3" })
                {
                    if (doc.RootElement.TryGetProperty(target, out var rate))
                    {
                        return Math.Clamp(rate.GetDouble(), 1.0, 200.0);
                    }
                }
            }
        }
        catch
        {
            // fall through to the default
        }

        return 2.0;
    }
}
