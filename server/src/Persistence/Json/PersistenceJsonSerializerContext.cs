// <copyright file="PersistenceJsonSerializerContext.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Json;

using System.Text.Json.Serialization;
using HNMO.AttributeSystem;

/// <summary>
/// The json metadata of the types which are read by the reference resolving converters, but which are
/// not persistent types themselves, so they're not provided by the <see cref="ReferenceResolvingTypeInfoResolver"/>.
/// </summary>
[JsonSerializable(typeof(SimpleElement))]
internal partial class PersistenceJsonSerializerContext : JsonSerializerContext
{
}
