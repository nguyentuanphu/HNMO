// <copyright file="AllianceCreationArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for creating an alliance.
/// </summary>
public record AllianceCreationArguments(uint MasterGuildId, uint TargetGuildId);
