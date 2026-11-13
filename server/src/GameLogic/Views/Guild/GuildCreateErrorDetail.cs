// <copyright file="GuildCreateErrorDetail.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Guild;

/// <summary>
/// Guild create error detail code.
/// </summary>
public enum GuildCreateErrorDetail : byte
{
    /// <summary>
    /// No error occured.
    /// </summary>
    None,

    /// <summary>
    /// The guild already exists.
    /// </summary>
    GuildAlreadyExist,
}