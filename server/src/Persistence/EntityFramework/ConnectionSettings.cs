// <copyright file="ConnectionSettings.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Runtime.Serialization;
using System.Xml.Serialization;

/// <summary>
/// The database connection settings xml serialization class.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "https://github.com/nguyentuanphu/HNMO/ConnectionSettings")]
[XmlRoot(Namespace = "https://github.com/nguyentuanphu/HNMO/ConnectionSettings", IsNullable = false)]
public class ConnectionSettings : IDeserializationCallback
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettings"/> class.
    /// </summary>
    public ConnectionSettings()
    {
        this.Connections = new ConnectionSetting[0];
    }

    /// <summary>
    /// Gets or sets the database connections.
    /// </summary>
    [XmlArray(IsNullable = false)]
    [XmlArrayItem("Connection", IsNullable = false)]
    public ConnectionSetting[] Connections { get; set; }

    /// <summary>
    /// Runs when the entire object graph has been deserialized.
    /// </summary>
    /// <param name="sender">The object that initiated the callback. The functionality for this parameter is not currently implemented.</param>
    public void OnDeserialization(object? sender)
    {
        this.Connections ??= new ConnectionSetting[0];
    }
}