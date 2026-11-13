// <copyright file="GameConfigurationJsonObjectLoader.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Json;

using HNMO.Persistence.EntityFramework.Model;
using HNMO.Persistence.Json;

/// <summary>
/// Object loader for <see cref="GameConfiguration"/> objects.
/// </summary>
public class GameConfigurationJsonObjectLoader : JsonObjectLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameConfigurationJsonObjectLoader"/> class.
    /// </summary>
    public GameConfigurationJsonObjectLoader()
        : base(
            new GameConfigurationJsonQueryBuilder(),
            new JsonObjectDeserializer(),
            new IdReferenceHandler())
    {
    }
}