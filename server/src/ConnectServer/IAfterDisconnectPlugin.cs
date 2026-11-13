// <copyright file="IAfterDisconnectPlugin.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ConnectServer;

/// <summary>
/// Plugin which is executed after a client disconnected.
/// </summary>
internal interface IAfterDisconnectPlugin
{
    /// <summary>
    /// Called after a client disconnected.
    /// </summary>
    /// <param name="client">The client.</param>
    void OnAfterDisconnect(Client client);
}