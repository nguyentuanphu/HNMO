// <copyright file="PlayerLicenseTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text;
using HNMO.GameLogic;
using HNMO.Integrity;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

/// <summary>
/// The player limit's license (license.hnmo) and the count of players in the game.
/// </summary>
[TestFixture]
public class PlayerLicenseTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);
    private Ed25519PrivateKeyParameters _privateKey = null!;
    private byte[] _publicKeyPem = null!;

    /// <summary>A fresh keypair for each test.</summary>
    [SetUp]
    public void SetUp()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        this._privateKey = (Ed25519PrivateKeyParameters)pair.Private;
        using var writer = new StringWriter();
        new PemWriter(writer).WriteObject(pair.Public);
        this._publicKeyPem = Encoding.UTF8.GetBytes(writer.ToString());
        LicensedPlayerLimit.Reset();
    }

    /// <summary>No file: the default of 10.</summary>
    [Test]
    public void WithoutLicenseTheDefaultApplies()
    {
        var license = PlayerLicense.Check(null, this._publicKeyPem, "abc", Today);
        Assert.That(license.IsValid, Is.False);
        Assert.That(license.MaximumPlayers, Is.EqualTo(10));
    }

    /// <summary>A signed license raises the limit.</summary>
    [Test]
    public void SignedLicenseRaisesTheLimit()
    {
        var license = PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":"2027-01-31","machine":null}"""), this._publicKeyPem, "abc", Today);
        Assert.That(license.IsValid, Is.True);
        Assert.That(license.MaximumPlayers, Is.EqualTo(50));
        Assert.That(license.Licensee, Is.EqualTo("Minh"));
    }

    /// <summary>A changed payload, an expired license, another computer or a license of another key: the default.</summary>
    [Test]
    public void InvalidLicensesFallBackToTheDefault()
    {
        var tampered = Encoding.UTF8.GetString(this.File("""{"licensee":"Minh","maxPlayers":5,"expires":null,"machine":null}"""))
            .Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"licensee":"Minh","maxPlayers":5,"expires":null,"machine":null}""")), Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"licensee":"Minh","maxPlayers":500,"expires":null,"machine":null}""")), StringComparison.Ordinal);
        Assert.That(PlayerLicense.Check(Encoding.UTF8.GetBytes(tampered), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "tampered");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":"2026-11-14","machine":null}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "expired");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":"other"}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "other computer");
        Assert.That(PlayerLicense.Check(this.File("""{"licensee":"Minh","maxPlayers":50,"expires":null,"machine":"ABC"}"""), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(50), "this computer");
        Assert.That(PlayerLicense.Check(Encoding.UTF8.GetBytes("not json"), this._publicKeyPem, "abc", Today).MaximumPlayers, Is.EqualTo(10), "garbage");
    }

    /// <summary>The count is per account and stops at the maximum.</summary>
    [Test]
    public void TheLimitCountsAccounts()
    {
        LicensedPlayerLimit.Maximum = 2;
        try
        {
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "a"), Is.True);
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "b"), Is.True);
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "c"), Is.False, "the third account");
            Assert.That(LicensedPlayerLimit.TryAdmit(null!, "A"), Is.True, "the same account again");
            Assert.That(LicensedPlayerLimit.Count, Is.EqualTo(2));
        }
        finally
        {
            LicensedPlayerLimit.Maximum = LicensedPlayerLimit.DefaultMaximum;
            LicensedPlayerLimit.Reset();
        }
    }

    private byte[] File(string payloadJson)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var signer = new Ed25519Signer();
        signer.Init(true, this._privateKey);
        signer.BlockUpdate(payload, 0, payload.Length);
        var signature = signer.GenerateSignature();
        return Encoding.UTF8.GetBytes($$"""{"payload":"{{Convert.ToBase64String(payload)}}","signature":"{{Convert.ToBase64String(signature)}}"}""");
    }
}
