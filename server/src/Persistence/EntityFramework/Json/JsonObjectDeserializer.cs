// <copyright file="JsonObjectDeserializer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Json;

using System.Text.Json;
using HNMO.PlugIns;

/// <summary>
/// A deserializer which parses the json retrieved from the postgres database by using a query built by the <see cref="JsonQueryBuilder"/>.
/// We need to register special converters, because postgres provides binary data and intervals in non-standard formats.
/// </summary>
public class JsonObjectDeserializer : HNMO.Persistence.Json.JsonObjectDeserializer
{
    /// <inheritdoc/>
    protected override void BeforeDeserialize(JsonSerializerOptions options)
    {
        base.BeforeDeserialize(options);
        options.Converters.Add(new IntervalAsTimeSpanJsonConverter());
        foreach (var converter in JsonConverterRegistry.Converters)
        {
            options.Converters.Add(converter);
        }
    }
}