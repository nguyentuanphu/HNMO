// <copyright file="DbLogRetentionRegistry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>Process-wide registration for <see cref="IDbLogRetentionExporter"/>.</summary>
public static class DbLogRetentionRegistry
{
    /// <summary>Gets or sets the active exporter implementation, if PostgreSQL persistence is configured.</summary>
    public static IDbLogRetentionExporter? Current { get; set; }
}
