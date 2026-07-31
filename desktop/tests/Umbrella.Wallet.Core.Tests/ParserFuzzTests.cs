using Umbrella.Wallet.Core.Derivation;
using Umbrella.Wallet.Core.Ton;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// A deterministic fuzz / robustness harness for every parser that touches untrusted input: a pasted
/// recipient address, a base58 blob, or a bag of cell bytes. It throws tens of thousands of random and
/// mutated inputs at each parser and asserts they always fail *cleanly* — returning a value or throwing
/// a "bad input" exception (<see cref="ArgumentException"/> / <see cref="FormatException"/>), never a
/// crash-class exception (IndexOutOfRange, NullReference, Overflow, OutOfMemory) and never a hang.
///
/// This is the class of bug that a byte-oriented parser hides until someone feeds it hostile data:
/// <c>TonCell.FromBoc</c> used to index straight into the buffer and allocate from an unchecked count,
/// so a truncated BoC crashed instead of being rejected. Seeds are fixed, so a failure is reproducible.
///
/// For deeper, coverage-guided fuzzing locally, these same entry points can be driven by SharpFuzz +
/// libFuzzer; this harness is the always-on, cross-platform, CI-friendly floor under that.
/// </summary>
public sealed class ParserFuzzTests
{
    private const int Iterations = 40_000;

    /// <summary>Only "this input is bad" failures are acceptable. Anything else is a robustness bug.</summary>
    private static void AssertFailsCleanly(Action parse, Func<string> describe)
    {
        try
        {
            parse();
        }
        catch (ArgumentException) { /* clean rejection */ }
        catch (FormatException) { /* clean rejection */ }
        catch (Exception ex)
        {
            Assert.Fail($"Parser threw {ex.GetType().Name} (not a clean rejection) on input: {describe()}\n{ex}");
        }
    }

    [Fact]
    public void FromBoc_never_crashes_on_random_or_mutated_input()
    {
        var rng = new Random(1337);
        var validBoc = TonTransfer.CodeCell().ToBoc();

        for (var it = 0; it < Iterations; it++)
        {
            byte[] input;
            if (it % 3 == 0)
            {
                // Wholly random bytes, sometimes carrying the real BoC magic so we get deeper into the parser.
                input = new byte[rng.Next(0, 512)];
                rng.NextBytes(input);
                if (input.Length >= 4 && rng.Next(2) == 0)
                {
                    input[0] = 0xB5; input[1] = 0xEE; input[2] = 0x9C; input[3] = 0x72;
                }
            }
            else
            {
                // Mutations of a valid BoC: bit-flips, truncation, and random-byte splices — the inputs
                // most likely to slip past a shallow magic check and reach the descriptor/index logic.
                input = (byte[])validBoc.Clone();
                var mutations = rng.Next(1, 6);
                for (var m = 0; m < mutations; m++) input[rng.Next(input.Length)] = (byte)rng.Next(256);
                if (rng.Next(3) == 0 && input.Length > 6) input = input[..rng.Next(5, input.Length)];
            }

            var captured = input;
            AssertFailsCleanly(() => TonCell.FromBoc(captured), () => Convert.ToHexString(captured));
        }

        // Explicit boundary inputs a random walk rarely hits exactly.
        foreach (var edge in new[]
                 {
                     Array.Empty<byte>(),
                     new byte[] { 0xB5, 0xEE, 0x9C, 0x72 },                 // magic only
                     new byte[] { 0xB5, 0xEE, 0x9C, 0x72, 0x00 },          // magic + flags, then nothing
                     new byte[] { 0xB5, 0xEE, 0x9C, 0x72, 0xFF, 0xFF, 0xFF, 0xFF }, // absurd sizes/counts
                 })
        {
            AssertFailsCleanly(() => TonCell.FromBoc(edge), () => Convert.ToHexString(edge));
        }

        // A valid BoC must still parse — the harness proves we reject junk, not everything.
        Assert.Equal(TonTransfer.CodeCell().HashHex(), TonCell.FromBoc(validBoc).HashHex());
    }

    [Fact]
    public void ParseFriendlyAddress_never_crashes_on_random_or_mutated_input()
    {
        const string valid = "UQAUGur5EsUnRjaGADHOuNpsaTgto82lGpzbfyLm11ymwHON";
        const string alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_=+/ \t";
        var rng = new Random(4242);

        for (var it = 0; it < Iterations; it++)
        {
            string input;
            if (it % 2 == 0)
            {
                var len = rng.Next(0, 60);
                var chars = new char[len];
                for (var i = 0; i < len; i++) chars[i] = alphabet[rng.Next(alphabet.Length)];
                input = new string(chars);
            }
            else
            {
                // Mutate a real address: swap a few characters so it still looks address-shaped.
                var chars = valid.ToCharArray();
                var swaps = rng.Next(1, 5);
                for (var s = 0; s < swaps; s++) chars[rng.Next(chars.Length)] = alphabet[rng.Next(alphabet.Length)];
                input = new string(chars);
            }

            var captured = input;
            AssertFailsCleanly(() => TonTransfer.ParseFriendlyAddress(captured), () => $"\"{captured}\"");
        }

        // The genuine address parses and its checksum validates.
        var (wc, hash, _) = TonTransfer.ParseFriendlyAddress(valid);
        Assert.Equal(0, wc);
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Monero_DecodeBase58_never_crashes_on_random_input()
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz!@# 0OIl";
        var rng = new Random(9001);

        for (var it = 0; it < Iterations; it++)
        {
            var len = rng.Next(0, 40);
            var chars = new char[len];
            for (var i = 0; i < len; i++) chars[i] = alphabet[rng.Next(alphabet.Length)];
            var input = new string(chars);

            AssertFailsCleanly(() => MoneroKeys.DecodeBase58(input), () => $"\"{input}\"");
        }

        // A round-trip through encode/decode must survive.
        var payload = new byte[32];
        new Random(1).NextBytes(payload);
        Assert.Equal(payload, MoneroKeys.DecodeBase58(MoneroKeys.EncodeBase58(payload)));
    }
}
