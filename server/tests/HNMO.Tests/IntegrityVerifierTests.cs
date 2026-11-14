// <copyright file="IntegrityVerifierTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HNMO.Integrity;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

/// <summary>
/// Tests the HNMO build integrity verifier: a good signed manifest verifies, a tampered file
/// is detected, and a missing signature warns and continues.
/// </summary>
[TestFixture]
public class IntegrityVerifierTests
{
    private readonly Dictionary<string, byte[]> _files = new()
    {
        ["server/HNMO.GameServer.dll"] = Encoding.UTF8.GetBytes("game server bytes"),
        ["web/wwwroot/js/main.min.js"] = Encoding.UTF8.GetBytes("console.log('hnmo')"),
    };

    private AsymmetricCipherKeyPair _keyPair = null!;
    private byte[] _publicKeyPem = null!;

    /// <summary>
    /// Generates a fresh Ed25519 keypair for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        this._keyPair = generator.GenerateKeyPair();
        this._publicKeyPem = ExportPublicKeyPem(this._keyPair.Public);
    }

    /// <summary>
    /// A manifest whose signature verifies and whose files all match reports <see cref="IntegrityStatus.Ok"/>.
    /// </summary>
    [Test]
    public void GoodManifestVerifies()
    {
        var manifest = this.BuildManifest(this._files);
        var signature = this.Sign(manifest);

        var result = IntegrityVerifier.Verify(manifest, signature, this._publicKeyPem, this.ReadFile);

        Assert.That(result.Status, Is.EqualTo(IntegrityStatus.Ok));
        Assert.That(result.ShouldRefuseStartup(true), Is.False);
    }

    /// <summary>
    /// If a file no longer matches its hash in the signed manifest, verification fails with
    /// <see cref="IntegrityStatus.FileMismatch"/> and the file is reported.
    /// </summary>
    [Test]
    public void TamperedFileFails()
    {
        var manifest = this.BuildManifest(this._files);
        var signature = this.Sign(manifest);
        var tampered = new Dictionary<string, byte[]>(this._files)
        {
            ["web/wwwroot/js/main.min.js"] = Encoding.UTF8.GetBytes("console.log('rebranded')"),
        };

        var result = IntegrityVerifier.Verify(manifest, signature, this._publicKeyPem, path => tampered.GetValueOrDefault(path));

        Assert.That(result.Status, Is.EqualTo(IntegrityStatus.FileMismatch));
        Assert.That(result.MismatchedFiles, Has.Count.EqualTo(1));
        Assert.That(result.ShouldRefuseStartup(true), Is.True);
        Assert.That(result.ShouldRefuseStartup(false), Is.False);
    }

    /// <summary>
    /// A missing signature yields <see cref="IntegrityStatus.Unsigned"/>, which warns and lets
    /// the server continue (a development build).
    /// </summary>
    [Test]
    public void MissingSignatureWarnsAndContinues()
    {
        var manifest = this.BuildManifest(this._files);

        var result = IntegrityVerifier.Verify(manifest, null, this._publicKeyPem, this.ReadFile);

        Assert.That(result.Status, Is.EqualTo(IntegrityStatus.Unsigned));
        Assert.That(result.ShouldRefuseStartup(true), Is.False);
    }

    /// <summary>
    /// A present-but-invalid signature (e.g. a doctored manifest) yields
    /// <see cref="IntegrityStatus.SignatureInvalid"/> and always refuses startup.
    /// </summary>
    [Test]
    public void InvalidSignatureRefuses()
    {
        var manifest = this.BuildManifest(this._files);
        var signature = this.Sign(manifest);
        manifest[10] ^= 0xFF;

        var result = IntegrityVerifier.Verify(manifest, signature, this._publicKeyPem, this.ReadFile);

        Assert.That(result.Status, Is.EqualTo(IntegrityStatus.SignatureInvalid));
        Assert.That(result.ShouldRefuseStartup(false), Is.True);
    }

    private static byte[] ExportPublicKeyPem(AsymmetricKeyParameter publicKey)
    {
        using var writer = new StringWriter();
        new PemWriter(writer).WriteObject(publicKey);
        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private byte[] BuildManifest(IReadOnlyDictionary<string, byte[]> files)
    {
        var builder = new StringBuilder();
        builder.Append("{\"projectId\":\"test\",\"buildStamp\":\"HNMO-test-20261114\",\"files\":[");
        var first = true;
        foreach (var (path, bytes) in files)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            builder.Append("{\"path\":\"").Append(path).Append("\",\"sha256\":\"").Append(hash).Append("\"}");
        }

        builder.Append("]}");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] Sign(byte[] message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, this._keyPair.Private);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }

    private byte[]? ReadFile(string path) => this._files.GetValueOrDefault(path);
}
