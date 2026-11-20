// <copyright file="ErrorCountingSink.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Startup;

using HNMO.GameLogic;
using Serilog.Core;
using Serilog.Events;

/// <summary>Counts the log's errors (Error and Fatal) for the weekly report to the HNMO developer.</summary>
internal sealed class ErrorCountingSink : ILogEventSink
{
    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level >= LogEventLevel.Error)
        {
            DeveloperHub.CountError();
        }
    }
}
