using System.Text.Json;
using BlackChannel.Shared;
using Xunit;

namespace BlackChannel.Tests;

/// <summary>
/// These guard the wire contract between the Blazor client (and its JS crypto) and the
/// Functions backend. The JS in wwwroot/js/crypto.js returns { eph, iv, ct }, and the
/// default System.Text.Json options on both ends are camelCase — so a SealedEnvelope must
/// serialize/deserialize cleanly with camelCase property names.
/// </summary>
public class ContractTests
{
    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SealedEnvelope_round_trips_camelCase()
    {
        var env = new SealedEnvelope
        {
            Id = "abc123",
            From = "alice",
            To = "bob",
            EphemeralPublicKey = "BHk_eph",
            Iv = "aXY=",
            Ciphertext = "Y3Q="
        };

        var json = JsonSerializer.Serialize(env, CamelCase);
        Assert.Contains("\"ephemeralPublicKey\"", json);
        Assert.Contains("\"ciphertext\"", json);

        var back = JsonSerializer.Deserialize<SealedEnvelope>(json, CamelCase)!;
        Assert.Equal(env.To, back.To);
        Assert.Equal(env.EphemeralPublicKey, back.EphemeralPublicKey);
        Assert.Equal(env.Ciphertext, back.Ciphertext);
    }

    [Fact]
    public void KeyBundle_ratchet_fields_default_empty_for_baseline()
    {
        // The ECIES baseline only populates IdentityPublicKey; the X3DH/Double-Ratchet
        // fields exist on the contract but stay empty until that upgrade.
        var bundle = new KeyBundle { UserId = "alice", IdentityPublicKey = "pub" };

        Assert.Null(bundle.SignedPreKey);
        Assert.Empty(bundle.OneTimePreKeys);
    }
}
