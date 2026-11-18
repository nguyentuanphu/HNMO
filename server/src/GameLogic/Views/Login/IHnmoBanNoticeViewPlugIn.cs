// <copyright file="IHnmoBanNoticeViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Login;

/// <summary>Sends structured bilingual ban or mute details (reason and Sydney expiration time) to the player.</summary>
public interface IHnmoBanNoticeViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the serialized ban or mute notice JSON packet.</summary>
    /// <param name="json">The JSON payload describing reasonEn, reasonVi, and Sydney expiration time.</param>
    /// <returns>The send task.</returns>
    ValueTask SendBanNoticeAsync(string json);
}
