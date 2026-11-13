// <copyright file="ConfigurationChangeArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Dapr.Common;

/// <summary>
/// Arguments for the change notifications of <see cref="ConfigurationChangePublisher"/>.
/// </summary>
public record class ConfigurationChangeArguments(Type Type, Guid Id, object? Configuration);