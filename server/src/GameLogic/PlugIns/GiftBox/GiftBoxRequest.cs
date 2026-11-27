// <copyright file="GiftBoxRequest.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>A bounded own-account mailbox selection; rewards and account identity are never supplied by a player.</summary>
/// <param name="Action">State or collect.</param>
/// <param name="Offset">The bounded mailbox page offset.</param>
/// <param name="LetterId">The optional existing letter to inspect or collect.</param>
/// <param name="CharacterId">The owned collection target; live adapters require the currently selected character.</param>
/// <param name="Language">English or Vietnamese display text.</param>
public sealed record GiftBoxRequest(string Action, int Offset = 0, Guid? LetterId = null, Guid? CharacterId = null, string Language = "en")
{
    /// <summary>Gets a value indicating whether the request contains only supported bounded selections.</summary>
    public bool IsValid => this.Offset is >= 0 and <= 100000 && this.Language is "en" or "vi"
        && this.LetterId != Guid.Empty && this.CharacterId != Guid.Empty
        && (this.Action == "state" || (this.Action == "collect" && this.LetterId.HasValue && this.CharacterId.HasValue));
}
