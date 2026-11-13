// <copyright file="StartupCancelledException.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Startup;

/// <summary>
/// Exception that is thrown when the user declines an update which is required to start the server.
/// </summary>
internal sealed class StartupCancelledException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupCancelledException"/> class.
    /// </summary>
    /// <param name="message">The message which says what was cancelled.</param>
    public StartupCancelledException(string message)
        : base(message)
    {
    }
}
