// <copyright file="AllianceChangedArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for the notification that a guild joined or left an alliance.
/// </summary>
public record AllianceChangedArguments(uint MasterGuildId, uint MemberGuildId);
