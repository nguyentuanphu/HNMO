// <copyright file="AssetOperationTestBarrier.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.IO;
using System.Threading;

/// <summary>
/// A stop point for the crash tests of the asset operations (tests/asset-crash-harness): when the environment variable
/// <c>HNMO_TEST_ASSET_BARRIER</c> names a stage (<c>before-save</c>, <c>before-commit</c>, <c>after-commit</c>), the
/// operation writes <c>HNMO_TEST_ASSET_BARRIER_FILE</c> on reaching it and waits there to be killed, or until the file
/// <c>HNMO_TEST_ASSET_BARRIER_GO</c> exists. Without the variable, nothing happens.
/// </summary>
internal static class AssetOperationTestBarrier
{
    private static readonly string? Stage = Environment.GetEnvironmentVariable("HNMO_TEST_ASSET_BARRIER");

    /// <summary>Stops at <paramref name="stage"/> when the crash test asks for it.</summary>
    /// <param name="stage">The stage reached.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public static async ValueTask ReachAsync(string stage, CancellationToken cancellationToken)
    {
        if (Stage is null || !Matches(Stage, stage))
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("HNMO_TEST_ASSET_BARRIER_FILE") is { Length: > 0 } file)
        {
            await File.WriteAllTextAsync(file, stage, cancellationToken).ConfigureAwait(false);
        }

        // With a "go" file, the operation goes on once it exists (the test cuts the database connection meanwhile);
        // without one, it waits to be killed.
        if (Environment.GetEnvironmentVariable("HNMO_TEST_ASSET_BARRIER_GO") is { Length: > 0 } go)
        {
            while (!File.Exists(go))
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }

    private static bool Matches(string configured, string reached)
    {
        if (string.Equals(configured, reached, StringComparison.Ordinal))
        {
            return true;
        }

        return (configured, reached) switch
        {
            ("before-local-commit", "before-commit") => true,
            ("after-local-commit", "after-commit") => true,
            _ => false,
        };
    }
}
