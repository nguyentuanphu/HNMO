// <copyright file="ISpanEncryptor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Network;

/// <summary>
/// This interface specifies an encryptor, which encrypts span data packets in-place.
/// </summary>
public interface ISpanEncryptor
{
    /// <summary>
    /// Encrypts the packet.
    /// </summary>
    /// <param name="packet">The decrypted data packet.</param>
    void Encrypt(Span<byte> packet);
}