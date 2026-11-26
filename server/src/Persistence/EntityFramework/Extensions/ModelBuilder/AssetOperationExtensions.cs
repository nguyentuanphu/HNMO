// <copyright file="AssetOperationExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Extensions.ModelBuilder;

using HNMO.Persistence.EntityFramework.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Extensions for the <see cref="EntityTypeBuilder"/>s of the Jewel Bank balances and the asset operation receipts.
/// </summary>
internal static class AssetOperationExtensions
{
    /// <summary>
    /// Applies the settings for the <see cref="JewelBankBalance"/> entity: one row per account and jewel kind.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<JewelBankBalance> builder)
    {
        builder.Property(balance => balance.JewelKey).HasMaxLength(64);
        builder.HasIndex(balance => new { balance.AccountId, balance.JewelKey }).IsUnique();
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(balance => balance.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>
    /// Applies the settings for the <see cref="AssetOperation"/> entity; its key is the caller's operation identifier
    /// (generated only when none is given).
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static void Apply(this EntityTypeBuilder<AssetOperation> builder)
    {
        builder.Property(operation => operation.Kind).HasMaxLength(64);
        builder.Property(operation => operation.PayloadHash).HasMaxLength(128);
        builder.Property(operation => operation.Source).HasMaxLength(32);
        builder.Property(operation => operation.PendingKey).HasMaxLength(64);
        builder.HasIndex(operation => new { operation.AccountId, operation.PendingCount });
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(operation => operation.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
