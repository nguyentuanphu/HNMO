// <copyright file="VipRemovalReceipt.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Host removal proof saved together with expiry of the owned grants; contains private operational attribution.</summary>
/// <param name="Kind">The versioned format identifier.</param>
/// <param name="Intent">The account-bound authorized removal.</param>
public sealed record VipRemovalReceipt(string Kind, VipRemovalIntent Intent);
