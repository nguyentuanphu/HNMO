// <copyright file="PlugInSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
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
    public static Task<T?> ReadActiveAsync<T>(IPersistenceContextProvider contextProvider, Type plugInType, CancellationToken cancellationToken)
        where T : class
        => ReadActiveAsync<T>(contextProvider, plugInType, null, cancellationToken);

    /// <summary>
    /// Reads the saved settings of an active plug-in; when it's active but its settings were never saved,
    /// <paramref name="defaults"/> gives what the plug-in itself uses then.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="plugInType">The plug-in type.</param>
    /// <param name="defaults">The settings of an active plug-in without saved settings, or null for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings, or null when the plug-in is off.</returns>
    public static async Task<T?> ReadActiveAsync<T>(IPersistenceContextProvider contextProvider, Type plugInType, Func<T>? defaults, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var context = contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
            var configurations = await context.GetAsync<PlugInConfiguration>(cancellationToken).ConfigureAwait(false);
            var configuration = configurations.FirstOrDefault(c => c.TypeId == plugInType.GUID);
            if (configuration is null)
            {
                // A plug-in without a saved row is active (plug-ins are active when registered).
                return defaults?.Invoke();
            }

            if (!configuration.IsActive)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(configuration.CustomConfiguration))
            {
                return defaults?.Invoke();
            }

            return JsonSerializer.Deserialize<T>(WithoutReferences(JsonNode.Parse(configuration.CustomConfiguration)), Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a plug-in's row straight from the database (not the admin panel's cached configuration, which
    /// can miss a row the game server added after the panel loaded it).
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="contextProvider">The persistence context provider.</param>
    /// <param name="plugInType">The plug-in type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the row exists, whether it's active, and its settings (null when none were saved).</returns>
    public static async Task<(bool Exists, bool IsActive, T? Settings)> ReadStoredAsync<T>(IPersistenceContextProvider contextProvider, Type plugInType, CancellationToken cancellationToken)
        where T : class
    {
        using var context = contextProvider.CreateNewTypedContext(typeof(PlugInConfiguration), false);
        var configurations = await context.GetAsync<PlugInConfiguration>(cancellationToken).ConfigureAwait(false);
        var configuration = configurations.FirstOrDefault(c => c.TypeId == plugInType.GUID);
        if (configuration is null)
        {
            return (false, false, null);
        }

        var settings = string.IsNullOrWhiteSpace(configuration.CustomConfiguration)
            ? null
            : JsonSerializer.Deserialize<T>(WithoutReferences(JsonNode.Parse(configuration.CustomConfiguration)), Options);
        return (true, configuration.IsActive, settings);
    }

    /// <summary>
    /// Drops the reference markers the admin panel's editor saves ("$id", "$ref", and lists as "$values"),
    /// so the plain settings can be read.
    /// </summary>
    private static JsonNode? WithoutReferences(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when obj["$values"] is JsonArray values:
                return WithoutReferences(values.DeepClone());
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    if (key is "$id" or "$ref")
                    {
                        continue;
                    }

                    result[key] = WithoutReferences(value?.DeepClone());
                }

                return result;
            case JsonArray array:
                var list = new JsonArray();
                foreach (var item in array)
                {
                    list.Add(WithoutReferences(item?.DeepClone()));
                }

                return list;
            default:
                return node;
        }
    }
}
