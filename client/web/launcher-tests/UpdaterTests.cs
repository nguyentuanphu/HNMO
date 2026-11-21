// Unit tests of the updater's rules (bridge/Updater.cs): which version is newer, which files an update brings and
// takes away, and that a manifest can't reach outside the HNMO folder or into the save game.
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class UpdaterTests
{
    [TestCase("1.0.1", "1.0.0", true)]
    [TestCase("1.0.10", "1.0.9", true, Description = "numbers, not text")]
    [TestCase("1.1.0", "1.0.99", true)]
    [TestCase("1.0.0", "1.0.0", false)]
    [TestCase("0.9.0", "1.0.0", false)]
    [TestCase(null, "1.0.0", false)]
    [TestCase("latest", "1.0.0", false)]
    public void NewerVersions(string? offered, string running, bool newer) => Assert.That(HnmoUpdater.IsNewer(offered, running), Is.EqualTo(newer));

    [Test]
    public void AnUpdateBringsTheChangedFilesAndTakesAwayTheGoneOnes()
    {
        var installed = new UpdateManifest("1.0.0", null, [new("server/a.dll", 1, "aa"), new("web/b.js", 1, "bb"), new("old.txt", 1, "cc")]);
        var next = new UpdateManifest("1.0.1", null, [new("server/a.dll", 1, "AA"), new("web/b.js", 1, "b2"), new("new.txt", 1, "dd")]);
        var (copies, removes) = HnmoUpdater.Diff(installed, next);
        Assert.That(copies.Select(c => c.Path), Is.EquivalentTo(new[] { "web/b.js", "new.txt" }), "the same hash in other letters is the same file");
        Assert.That(removes, Is.EqualTo(new[] { "old.txt" }));
    }

    [Test]
    public void AManifestStaysInsideTheFolderAndOutOfTheSaveGame()
    {
        var root = Path.Combine(Path.GetTempPath(), "hnmo-root");
        Assert.That(HnmoUpdater.SafePath(root, "server/HNMO.Startup.dll"), Is.EqualTo(Path.Combine(root, "server", "HNMO.Startup.dll")));
        Assert.That(HnmoUpdater.SafePath(root, "../outside.txt"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "server/../../outside.txt"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "save/hnmo-settings.json"), Is.Null, "the save game is never touched");
        Assert.That(HnmoUpdater.SafePath(root, "web/../save/x"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, "/etc/passwd"), Is.Null);
        Assert.That(HnmoUpdater.SafePath(root, ""), Is.Null);
    }

    [Test]
    public async Task AFilesHashIsItsSha256()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "HNMO");
        Assert.That(await HnmoUpdater.HashAsync(file), Is.EqualTo(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("HNMO"u8)).ToLowerInvariant()));
        File.Delete(file);
    }
}
