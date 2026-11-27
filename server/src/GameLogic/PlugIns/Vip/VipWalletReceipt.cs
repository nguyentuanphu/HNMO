// <copyright file="VipWalletReceipt.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>Proof saved in the same account transaction as the debit and timed grants.</summary>
/// <param name="Kind">The versioned format identifier.</param>
/// <param name="Intent">The authorized purchase.</param>
/// <param name="GrantIds">The exact grants owned by this purchase.</param>
public sealed record VipWalletReceipt(string Kind, VipWalletIntent Intent, List<Guid> GrantIds);
