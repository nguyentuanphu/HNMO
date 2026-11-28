// <copyright file="LatestReleaseCache.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

/// <summary>
/// The website's "Download HNMO" link as the hub's <c>/api/latest</c> gave it, kept for an hour (5 minutes after a
/// failure): each hub call is a paid function call (owner, 2026-11-28), and every visitor's click would make one.
/// </summary>
internal sealed class LatestReleaseCache
{
    /// <summary>How long a link is kept.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromHours(1);

    /// <summary>How long a failure is kept before the hub is asked again.</summary>
    public static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _link;
    private DateTime _until = DateTime.MinValue;

    /// <summary>Gets the link, asking the hub only when the kept answer is old.</summary>
    /// <param name="fetch">Asks the hub; null when it gave no link.</param>
    /// <param name="nowUtc">The time now.</param>
    /// <returns>The link, or null.</returns>
    public async Task<string?> GetAsync(Func<Task<string?>> fetch, DateTime nowUtc)
    {
        if (nowUtc < this._until)
        {
            return this._link;
        }

        await this._gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (nowUtc < this._until)
            {
                return this._link;
            }

            this._link = await fetch().ConfigureAwait(false);
            this._until = nowUtc + (this._link is null ? AfterFailure : Fresh);
            return this._link;
        }
        finally
        {
            this._gate.Release();
        }
    }
}
