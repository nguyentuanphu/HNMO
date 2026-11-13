// <copyright file="CustomIpResolver.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Network;

using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Resolver which always returns the provided custom ip address.
/// </summary>
public class CustomIpResolver : IIpAddressResolver
{
    private readonly IPAddress _address;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomIpResolver"/> class.
    /// </summary>
    /// <param name="address">The address.</param>
    public CustomIpResolver(IPAddress address)
    {
        this._address = address;
    }

    /// <inheritdoc />
    public ValueTask<IPAddress> ResolveIPv4Async() => ValueTask.FromResult(this._address);
}