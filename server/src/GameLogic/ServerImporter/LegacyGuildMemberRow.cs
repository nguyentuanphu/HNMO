// <copyright file="LegacyGuildMemberRow.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Guild membership row read from <c>GuildMember</c>.</summary>
public sealed class LegacyGuildMemberRow
{
    /// <summary>Gets or sets guild name.</summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>Gets or sets member character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets role rank code (e.g. 128=Master, 64=Assistant, 32=BattleMaster, 0=Normal).</summary>
    public byte Status { get; set; }
}
