// <copyright file="HnmoSkillsAdminOperationTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json.Nodes;
using System.Threading;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Administration;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Character;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>
/// Tests the live administration operation which grants and removes the HNMO skills of a character in the game,
/// and the removal of learned skills from the skill list.
/// </summary>
[TestFixture]
public class HnmoSkillsAdminOperationTest
{
    private const string CharacterName = "Hero";

    /// <summary>
    /// Granting the D skill teaches it to the live character: it's in the skill list, the client is told, and the
    /// player gets the message; granting it again changes nothing.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task GrantTeachesTheSkillAtOnceAsync()
    {
        var (player, plugIn) = await CreatePlayerAsync().ConfigureAwait(false);
        var skill = AddSkill(player, 703, "Charge", player.SelectedCharacter!.CharacterClass!);
        var arguments = Arguments(player, 703, true);

        var preview = await plugIn.PreviewAsync(player.GameContext, arguments, CancellationToken.None).ConfigureAwait(false);
        Assert.That(preview["learnedBefore"]!.GetValue<bool>(), Is.False);
        Assert.That(preview["learnedAfter"]!.GetValue<bool>(), Is.True);
        var result = await plugIn.ExecuteAsync(player.GameContext, arguments, preview, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result["changed"]!.GetValue<bool>(), Is.True);
        Assert.That(player.SkillList!.ContainsSkill(703), Is.True);
        Assert.That(player.SelectedCharacter.LearnedSkills.Select(s => s.Skill), Is.EqualTo(new[] { skill }));
        Mock.Get(player.ViewPlugIns.GetPlugIn<ISkillListViewPlugIn>()!).Verify(v => v!.AddSkillAsync(skill), Times.Once);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!).Verify(v => v!.ShowMessageAsync("You have learned Charge.", MessageType.BlueNormal), Times.Once);

        var again = await plugIn.ExecuteAsync(player.GameContext, arguments, null, CancellationToken.None).ConfigureAwait(false);
        Assert.That(again["changed"]!.GetValue<bool>(), Is.False);
        Assert.That(player.SelectedCharacter.LearnedSkills, Has.Count.EqualTo(1), "not learned twice");
    }

    /// <summary>
    /// Removing a learned skill takes it from the live character and the client.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task RemoveTakesTheSkillAtOnceAsync()
    {
        var (player, plugIn) = await CreatePlayerAsync().ConfigureAwait(false);
        var skill = AddSkill(player, 702, "Iron Guard", player.SelectedCharacter!.CharacterClass!);
        await player.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
        var arguments = Arguments(player, 702, false);

        var preview = await plugIn.PreviewAsync(player.GameContext, arguments, CancellationToken.None).ConfigureAwait(false);
        var result = await plugIn.ExecuteAsync(player.GameContext, arguments, preview, CancellationToken.None).ConfigureAwait(false);

        Assert.That(result["changed"]!.GetValue<bool>(), Is.True);
        Assert.That(result["learned"]!.GetValue<bool>(), Is.False);
        Assert.That(player.SkillList.ContainsSkill(702), Is.False);
        Assert.That(player.SelectedCharacter.LearnedSkills, Is.Empty);
        Mock.Get(player.ViewPlugIns.GetPlugIn<ISkillListViewPlugIn>()!).Verify(v => v!.RemoveSkillAsync(skill), Times.Once);
    }

    /// <summary>
    /// A skill of another class, a skill which isn't an HNMO skill, another character id and a preview which no longer
    /// matches are refused without a change.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InvalidRequestsAreRefusedAsync()
    {
        var (player, plugIn) = await CreatePlayerAsync().ConfigureAwait(false);
        AddSkill(player, 703, "Charge", player.SelectedCharacter!.CharacterClass!);
        AddSkill(player, 707, "Mana Shield", new Mock<CharacterClass>().Object);
        AddSkill(player, 41, "Twisting Slash", player.SelectedCharacter.CharacterClass!);

        Assert.ThrowsAsync<ArgumentException>(async () => await plugIn.PreviewAsync(player.GameContext, Arguments(player, 707, true), CancellationToken.None).ConfigureAwait(false), "another class");
        Assert.ThrowsAsync<ArgumentException>(async () => await plugIn.PreviewAsync(player.GameContext, Arguments(player, 41, true), CancellationToken.None).ConfigureAwait(false), "not an HNMO skill");
        var otherId = Arguments(player, 703, true);
        otherId["characterId"] = Guid.NewGuid().ToString();
        Assert.ThrowsAsync<ArgumentException>(async () => await plugIn.PreviewAsync(player.GameContext, otherId, CancellationToken.None).ConfigureAwait(false), "another character");

        var preview = await plugIn.PreviewAsync(player.GameContext, Arguments(player, 703, true), CancellationToken.None).ConfigureAwait(false);
        await player.SkillList!.AddLearnedSkillAsync(player.GameContext.Configuration.Skills.Single(s => s.Number == 703)).ConfigureAwait(false);
        Assert.ThrowsAsync<ArgumentException>(async () => await plugIn.ExecuteAsync(player.GameContext, Arguments(player, 703, true), preview, CancellationToken.None).ConfigureAwait(false), "stale preview");
        Assert.That(player.SelectedCharacter.LearnedSkills, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// The operation is valid for the live administration channel: its metadata match the plug-in.
    /// </summary>
    [Test]
    public void OperationMetadataMatchThePlugIn()
    {
        var plugIn = new HnmoSkillsAdminOperationPlugIn();
        Assert.That(plugIn.Operation.PlugInId, Is.EqualTo(typeof(HnmoSkillsAdminOperationPlugIn).GUID));
        Assert.That(plugIn.Operation.Name, Is.EqualTo(plugIn.Key));
        Assert.That(plugIn.Operation.ReadOnly, Is.False);
        Assert.That(plugIn.Operation.MinimumRole, Is.EqualTo("Operator"));
        Assert.That(plugIn.Operation.InputSchema["type"]!.GetValue<string>(), Is.EqualTo("object"));

        var coins = new CashShopCoinGrantsAdminOperationPlugIn();
        Assert.That(coins.Operation.PlugInId, Is.EqualTo(typeof(CashShopCoinGrantsAdminOperationPlugIn).GUID));
        Assert.That(coins.Operation.Name, Is.EqualTo(coins.Key));
        Assert.That(coins.Key.All(c => c is >= 'a' and <= 'z' or '_'), Is.True);
        Assert.That(plugIn.Key.All(c => c is >= 'a' and <= 'z' or '_'), Is.True);
    }

    /// <summary>
    /// The coin operation applies the pending grants of an account in the game at once.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CoinGrantsAreAppliedAtOnceAsync()
    {
        var (player, _) = await CreatePlayerAsync().ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { Id = Guid.NewGuid(), LoginName = "hero" };
        await player.GameContext.AddPlayerAsync(player).ConfigureAwait(false);
        using (var context = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration))
        {
            var grant = context.CreateNew<CashShopCoinGrant>();
            grant.AccountId = player.Account.GetId();
            grant.CoinType = CashShopCoinType.WCoinC;
            grant.Amount = 500;
            grant.CreatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var plugIn = new CashShopCoinGrantsAdminOperationPlugIn();
        var arguments = new JsonObject { ["loginName"] = "hero" };
        var preview = await plugIn.PreviewAsync(player.GameContext, arguments, CancellationToken.None).ConfigureAwait(false);
        var result = await plugIn.ExecuteAsync(player.GameContext, arguments, preview, CancellationToken.None).ConfigureAwait(false);

        Assert.That(player.Account.WCoinC, Is.EqualTo(500));
        Assert.That(result["wCoinC"]!.GetValue<int>(), Is.EqualTo(500));
        Assert.ThrowsAsync<ArgumentException>(async () => await plugIn.PreviewAsync(player.GameContext, new JsonObject { ["loginName"] = "nobody" }, CancellationToken.None).ConfigureAwait(false));
    }

    private static async ValueTask<(Player Player, HnmoSkillsAdminOperationPlugIn PlugIn)> CreatePlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.Name = CharacterName;
        player.SelectedCharacter.Id = Guid.NewGuid();
        ((GameContext)player.GameContext).PlayersByCharacterName.TryAdd(CharacterName, player);
        return (player, new HnmoSkillsAdminOperationPlugIn());
    }

    private static JsonObject Arguments(Player player, int skillNumber, bool learn) => new()
    {
        ["characterName"] = CharacterName,
        ["characterId"] = player.SelectedCharacter!.Id.ToString(),
        ["skillNumber"] = skillNumber,
        ["learn"] = learn,
    };

    private static Skill AddSkill(Player player, short number, string name, CharacterClass characterClass)
    {
        var skillMock = new Mock<Skill>();
        skillMock.SetupAllProperties();
        skillMock.Setup(s => s.QualifiedCharacters).Returns(new List<CharacterClass> { characterClass });
        var skill = skillMock.Object;
        skill.Number = number;
        skill.Name = name;
        skill.SkillType = SkillType.DirectHit;
        player.GameContext.Configuration.Skills.Add(skill);
        return skill;
    }
}
