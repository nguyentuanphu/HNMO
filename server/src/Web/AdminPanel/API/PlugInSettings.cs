// <copyright file="PlugInSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Text.Json;
using System.Threading;
using System.Text.Json.Serialization;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Reads a plug-in's saved configuration straight from the database, so that a change in the admin
/// panel counts at once for the public endpoints.
/// </summary>
internal static class PlugInSettings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Gets the saved configuration of a plug-in; <c>null</c> when it's deactivated or has none.
    /// </summary>
    /// <typeparam name="T">The configuration type.</typeparam>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="plugInType">The plug-in type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The configuration of an active plug-in, or <c>null</c>.</returns>
    public static async Task<T?> ReadActiveAsync<T>(IPersistenceContextProvider contextProvider, Type plugInType, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var context = contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
            var configurations = await context.GetAsync<PlugInConfiguration>(cancellationToken).ConfigureAwait(false);
            var configuration = configurations.FirstOrDefault(c => c.TypeId == plugInType.GUID);
            if (configuration is not { IsActive: true } || string.IsNullOrWhiteSpace(configuration.CustomConfiguration))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(configuration.CustomConfiguration, Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
