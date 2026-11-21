// <copyright file="QuestExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Extensions.ModelBuilder;

using HNMO.Persistence.EntityFramework.Model;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Extensions for the item-related <see cref="EntityTypeBuilder"/>.
/// </summary>
internal static class QuestExtensions
{
    /// <summary>
    /// Applies the settings for the <see cref="QuestDefinition"/> entity.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<QuestDefinition> builder)
    {
        builder.Property(p => p.Name).HasConversion(LocalizedStringConverter.Instance);
    }
}