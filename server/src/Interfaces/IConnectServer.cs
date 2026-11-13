// <copyright file="IConnectServer.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// The interface for a connect server.
/// </summary>
public interface IConnectServer : IManageableServer, IGameServerStateObserver
{
}