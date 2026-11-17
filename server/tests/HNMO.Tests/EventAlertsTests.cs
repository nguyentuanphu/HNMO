// <copyright file="EventAlertsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using HNMO.GameLogic.PlugIns.EventAlerts;
using HNMO.GameLogic.PlugIns.Timetable;

/// <summary>
/// Unit tests for <see cref="WebPushCrypto"/>, <see cref="EventAlertStore"/>, and <see cref="EventAlertsEngine"/>.
/// </summary>
[TestFixture]
public class EventAlertsTests
{
    /// <summary>Verifies VAPID P-256 keypair generation, ES256 JWT verification, and RFC 8291 encryption header.</summary>
    [Test]
    public void GeneratesValidVapidKeysSignsJwtAndEncryptsPayload()
    {
        var (pub, priv) = WebPushCrypto.GenerateVapidKeyPair();
        var pubBytes = WebPushCrypto.FromBase64Url(pub);
        var privBytes = WebPushCrypto.FromBase64Url(priv);
        Assert.That(pubBytes.Length, Is.EqualTo(65));
        Assert.That(pubBytes[0], Is.EqualTo(0x04));
        Assert.That(privBytes.Length, Is.EqualTo(32));

        var auth = WebPushCrypto.CreateVapidAuthorizationHeader(
            new Uri("https://fcm.googleapis.com/fcm/send/demo"),
            "mailto:admin@hnmo.local",
            pub,
            priv,
            DateTimeOffset.UtcNow);
        Assert.That(auth, Does.StartWith("vapid t="));
        Assert.That(auth, Does.EndWith($", k={pub}"));

        // Verify the ES256 JWT signature with the public key alone.
        var token = auth["vapid t=".Length..auth.IndexOf(", k=", StringComparison.Ordinal)];
        var parts = token.Split('.');
        Assert.That(parts.Length, Is.EqualTo(3));

        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = pubBytes[1..33], Y = pubBytes[33..65] },
        });
        var sigValid = verifier.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            WebPushCrypto.FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Assert.That(sigValid, Is.True);

        // Generate a synthetic UA subscription keypair and verify aes128gcm packet structure.
        var (uaPub, _) = WebPushCrypto.GenerateVapidKeyPair();
        var uaAuth = WebPushCrypto.ToBase64Url(RandomNumberGenerator.GetBytes(16));
        var encrypted = WebPushCrypto.EncryptPayloadAes128Gcm("{\"title\":\"Blood Castle\"}"u8, uaPub, uaAuth);
        Assert.That(encrypted.Length, Is.GreaterThan(16 + 4 + 1 + 65 + 16));
        Assert.That(encrypted[20], Is.EqualTo(65));
        Assert.That(encrypted[21], Is.EqualTo(0x04));
    }

    /// <summary>Verifies upcoming and opening alerts deduplicate cleanly and honor subscriber filters.</summary>
    [Test]
    public void CollectsDueAlertsOnceAndMatchesSubscriberPreferences()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"event-alerts-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = EventAlertStore.Open(tempFile);
            var cfg = new EventAlertsConfiguration
            {
                Enabled = true,
                FileName = tempFile,
                LeadTimeMinutes = 5,
                RemindAtStart = true,
            };

            var nowUtc = new DateTime(2026, 11, 17, 2, 0, 0, DateTimeKind.Utc);
            var snapshot = new TimetableSnapshot
            {
                Enabled = true,
                NowUtc = nowUtc,
                TimeZoneId = "Australia/Sydney",
                FindGmWinners = [],
                Entries =
                [
                    new TimetableComputedEntry
                    {
                        Id = "blood-castle",
                        Category = "event",
                        NameEn = "Blood Castle",
                        NameVi = "Huyết Lâu",
                        MapsEn = "Event Square",
                        MapsVi = "Quảng trường sự kiện",
                        DropsEn = string.Empty,
                        DropsVi = string.Empty,
                        ScheduleText = "Hourly",
                        DurationMinutes = 15,
                        IsActive = false,
                        NextStartUtc = nowUtc.AddMinutes(5),
                    },
                ],
            };

            var due = EventAlertsEngine.CollectDueAlerts(cfg, store, snapshot, nowUtc);
            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].CategoryKey, Is.EqualTo("blood_castle"));
            Assert.That(due[0].TitleEn, Does.Contain("Blood Castle"));
            Assert.That(due[0].TitleVi, Does.Contain("Huyết Lâu"));

            store.RecordSent(new EventAlertSentLog
            {
                SentKey = due[0].SentKey,
                TitleEn = due[0].TitleEn,
                TitleVi = due[0].TitleVi,
                SentAtUtc = nowUtc,
            });
            Assert.That(EventAlertsEngine.CollectDueAlerts(cfg, store, snapshot, nowUtc), Is.Empty);

            var bcSub = new EventAlertSubscription { Events = ["blood_castle"] };
            var bossSub = new EventAlertSubscription { Events = ["world_boss"] };
            Assert.That(EventAlertsEngine.MatchesSubscription(bcSub, "blood_castle"), Is.True);
            Assert.That(EventAlertsEngine.MatchesSubscription(bossSub, "blood_castle"), Is.False);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
