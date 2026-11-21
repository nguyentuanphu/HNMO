// <copyright file="NoOpServerRestartService.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HNMO.Interfaces;

/// <summary>
/// Fallback <see cref="IServerRestartService"/> when hosted outside <c>HNMO.Startup</c>.
/// </summary>
internal sealed class NoOpServerRestartService : IServerRestartService
{
    public bool IsSupported => false;

    public Task RequestRestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
