// <copyright file="VipRemovalReceipt.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Host removal proof saved together with expiry of the owned grants; contains private operational attribution.</summary>
/// <param name="Kind">The versioned format identifier.</param>
/// <param name="Intent">The account-bound authorized removal.</param>
public sealed record VipRemovalReceipt(string Kind, VipRemovalIntent Intent);
