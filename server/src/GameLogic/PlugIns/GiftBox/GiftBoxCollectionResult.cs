// <copyright file="GiftBoxCollectionResult.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftBox;

/// <summary>A machine-readable collection outcome for localized adapters.</summary>
/// <param name="Status">Applied, AlreadyCollected, Pending, or a refusal code.</param>
/// <param name="Receipt">The confirmed original proof, never a second reward.</param>
public sealed record GiftBoxCollectionResult(string Status, GiftBoxReceipt? Receipt = null);
