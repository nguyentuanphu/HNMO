// <copyright file="IHnmoUltimateAutoPlayViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends the resolved Ultimate Autoplay Free/VIP policy JSON (<c>F5 57</c>) to the web client.
/// </summary>
public interface IHnmoUltimateAutoPlayViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the policy JSON to the player's client.</summary>
    ValueTask ShowUltimateAutoPlayPolicyAsync(string json);
}
