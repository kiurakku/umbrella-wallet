using Umbrella.Wallet.Core.Derivation;

namespace Umbrella.Wallet.Core.Tests;

/// <summary>
/// Pins TON wallet v4R2 address derivation to the reference TON library (tonweb). The expected
/// strings were produced by tonweb for a public key of 32 bytes of 0x11 — if the cell hashing or
/// address encoding drifts by a single byte, the address changes and this fails. A wrong address
/// would make received TON unspendable, so this is a hard gate.
/// </summary>
public sealed class TonKeysTests
{
    private static byte[] Pub(byte fill)
    {
        var key = new byte[32];
        Array.Fill(key, fill);
        return key;
    }

    [Fact]
    public void V4r2_address_matches_tonweb_reference()
    {
        var pub = Pub(0x11);

        // tonweb: new WalletV4R2({publicKey, wc:0}).getAddress().toString(true, true, false/true)
        Assert.Equal(
            "UQCAwOrkCl6cPi_riCJAU3Bq3JzCsdGg3SA8_b-t76aqBtBv",
            TonKeys.WalletV4R2Address(pub, bounceable: false));
        Assert.Equal(
            "EQCAwOrkCl6cPi_riCJAU3Bq3JzCsdGg3SA8_b-t76aqBo2q",
            TonKeys.WalletV4R2Address(pub, bounceable: true));
    }
}
