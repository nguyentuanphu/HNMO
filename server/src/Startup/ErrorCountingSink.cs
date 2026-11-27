// <copyright file="ErrorCountingSink.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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
