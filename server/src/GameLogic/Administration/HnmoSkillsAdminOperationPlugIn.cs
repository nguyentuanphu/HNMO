// <copyright file="HnmoSkillsAdminOperationPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Administration;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using HNMO.GameLogic.Offline;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// The live administration operation which grants or removes one of the HNMO skills (700 to 727) of a character
/// which is in the game, e.g. the fourth skill of the class without doing its trial quest.
/// </summary>
/// <remarks>
/// The admin panel uses it for characters which are online; for offline characters it changes the stored character.
/// The skill appears in (or disappears from) the skill list of the game client at once.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoSkillsAdminOperationPlugIn_Name), Description = nameof(PlugInResources.HnmoSkillsAdminOperationPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E2B54C17-6A9D-4F38-B1C0-7D83A5F9E264")]
public sealed class HnmoSkillsAdminOperationPlugIn : IAdminOperationPlugIn
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public const string OperationName = "hnmo_skills_set";

    /// <inheritdoc />
    public string Key => OperationName;

    /// <inheritdoc />
    public AdminPluginOperationInfo Operation => new(
        typeof(HnmoSkillsAdminOperationPlugIn).GUID,
        OperationName,
        "Grants (learn=true) or removes (learn=false) one HNMO skill (700 to 727) of the class of a character which is in the game.",
        "Operator",
        ReadOnly: false,
        Destructive: false,
        Idempotent: true,
        new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("characterName", "characterId", "skillNumber", "learn"),
            ["properties"] = new JsonObject
            {
                ["characterName"] = new JsonObject { ["type"] = "string", ["maxLength"] = 10 },
                ["characterId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                ["skillNumber"] = new JsonObject { ["type"] = "integer", ["minimum"] = HnmoSkills.FirstSkillNumber, ["maximum"] = HnmoSkills.LastSkillNumber },
                ["learn"] = new JsonObject { ["type"] = "boolean" },
            },
        });

    /// <inheritdoc />
    public ValueTask<JsonObject> PreviewAsync(IGameContext context, JsonObject arguments, CancellationToken cancellationToken)
    {
        var request = Request.Parse(arguments);
        var (player, character, skill) = Resolve(context, request);
        return ValueTask.FromResult(CreatePreview(request, character, skill, player));
    }

    /// <inheritdoc />
    public async ValueTask<JsonObject> ExecuteAsync(IGameContext context, JsonObject arguments, JsonObject? expectedPreview, CancellationToken cancellationToken)
    {
        var request = Request.Parse(arguments);
        var (player, _, _) = Resolve(context, request);
        return await player.RunPersistenceExclusiveAsync(
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Checked again under the same lock as packet handlers and saves.
                var (_, character, skill) = Resolve(context, request);
                if (expectedPreview is not null && !JsonNode.DeepEquals(CreatePreview(request, character, skill, player), expectedPreview))
                {
                    throw new ArgumentException("The character changed. Request a new preview.", nameof(expectedPreview));
                }

                var learned = HnmoSkills.HasLearned(character, skill.Number);
                if (request.Learn && !learned)
                {
                    await player.SkillList!.AddLearnedSkillAsync(skill).ConfigureAwait(false);
                    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SkillLearned), skill.Name.GetTranslation(player.Culture)).ConfigureAwait(false);
                }
                else if (!request.Learn && learned)
                {
                    await player.SkillList!.RemoveLearnedSkillAsync(skill.Number.ToUnsigned()).ConfigureAwait(false);
                }

                var changed = request.Learn != learned;
                if (changed)
                {
                    await player.SaveProgressAsync(cancellationToken).ConfigureAwait(false);
                    player.Logger.LogInformation("An administrator {Change} the skill {Skill} of {Character}.", request.Learn ? "granted" : "removed", skill.Number, character.Name);
                }

                return new JsonObject
                {
                    ["characterName"] = character.Name,
                    ["skillNumber"] = (int)skill.Number,
                    ["learned"] = HnmoSkills.HasLearned(character, skill.Number),
                    ["changed"] = changed,
                };
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject CreatePreview(Request request, Character character, Skill skill, Player player) => new()
    {
        ["characterName"] = character.Name,
        ["characterId"] = character.Id.ToString(),
        ["skillNumber"] = (int)skill.Number,
        ["skillName"] = skill.Name.GetTranslation(player.Culture),
        ["learnedBefore"] = HnmoSkills.HasLearned(character, skill.Number),
        ["learnedAfter"] = request.Learn,
    };

    private static (Player Player, Character Character, Skill Skill) Resolve(IGameContext context, Request request)
    {
        if (context.GetPlayerByCharacterName(request.CharacterName) is not { } player
            || player is OfflinePlayer
            || !player.IsConnected
            || player.PlayerState.CurrentState != PlayerState.EnteredWorld
            || player.SelectedCharacter is not { CharacterClass: { } characterClass } character
            || player.SkillList is null
            || character.Id != request.CharacterId
            || !string.Equals(character.Name, request.CharacterName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The character is not in the game on this server.", nameof(request));
        }

        var skill = HnmoSkills.GetSkillsOf(context.Configuration, characterClass).FirstOrDefault(s => s.Number == request.SkillNumber)
            ?? throw new ArgumentException("The skill is not an HNMO skill of the character's class.", nameof(request));
        return (player, character, skill);
    }

    private sealed record Request(string CharacterName, Guid CharacterId, int SkillNumber, bool Learn)
    {
        public static Request Parse(JsonObject arguments)
        {
            try
            {
                var name = arguments["characterName"]?.GetValue<string>();
                var id = arguments["characterId"]?.GetValue<string>();
                var number = arguments["skillNumber"]?.GetValue<int>();
                var learn = arguments["learn"]?.GetValue<bool>();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 10 || !Guid.TryParse(id, out var characterId)
                    || number is not { } skillNumber || !HnmoSkills.IsHnmoSkill(skillNumber) || learn is not { } doLearn)
                {
                    throw new ArgumentException("Give characterName, characterId, skillNumber (700 to 727) and learn.", nameof(arguments));
                }

                return new Request(name, characterId, skillNumber, doLearn);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                throw new ArgumentException("Give characterName, characterId, skillNumber (700 to 727) and learn.", nameof(arguments), ex);
            }
        }
    }
}
