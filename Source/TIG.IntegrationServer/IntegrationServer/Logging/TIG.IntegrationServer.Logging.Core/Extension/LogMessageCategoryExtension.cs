using System.Diagnostics;
using TIG.IntegrationServer.Logging.Core.LogMessage.Enum;

namespace TIG.IntegrationServer.Logging.Core.Extension
{
    public static class LogMessageCategoryExtension
    {
        /// <summary>
        /// ToTraceEventType method for convert custom LogType to enterprise event type.
        /// </summary>
        /// <param name="logType">Log Type for log level.</param>
        /// <returns>Return trace event type.</returns>
        public static TraceEventType ToTraceEventType(this LogType logType)
        {
            switch (logType)
            {
                case LogType.Info:
                    return TraceEventType.Information;
                case LogType.Warn:
                    return TraceEventType.Warning;
                case LogType.Error:
                    return TraceEventType.Error;
                case LogType.Fatal:
                    return TraceEventType.Critical;
                default:
                    return TraceEventType.Verbose;
            }
        }
    }
}
