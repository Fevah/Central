using System;
using TIG.IntegrationServer.Logging.Core.LogMessage;
using TIG.IntegrationServer.Logging.Core.LogMessage.Enum;

namespace TIG.IntegrationServer.Logging.Core.Extension
{
    public static class LogInterfaceExtension
    {
        #region Info

        /// <summary>
        /// Write log as info level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Log message object for recording to system.</param>
        public static void Info(this ILog log, LogMessage.LogMessage message)
        {
            message.Type = LogType.Info;
            log.Message(message);
        }

        /// <summary>
        /// Write log as info level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for record to log system.</param>
        public static void Info(this ILog log, string message)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);
            var entry = new LogMessage.LogMessage
            {
                Body = body
            };

#if (DEBUG)
            Console.Write(body);
#endif
            log.Info(entry);
        }

        /// <summary>
        /// Write log as info level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for record to log system.</param>
        /// <param name="exception">Exception for record to log system.</param>
        public static void Info(this ILog log, string message, Exception exception)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);
            var entry = new ExceptionLogMessage
            {
                Body = body,
                Exception = exception
            };

#if (DEBUG)
            Console.Write(body);
#endif
            log.Info(entry);
        }

        #endregion


        #region Warn

        /// <summary>
        /// Write log as warn level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message object for record to system.</param>
        public static void Warn(this ILog log, LogMessage.LogMessage message)
        {
            message.Type = LogType.Warn;

            log.Message(message);
        }

        /// <summary>
        /// Write log as warn level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for record to system.</param>
        public static void Warn(this ILog log, string message)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);

            var entry = new LogMessage.LogMessage
            {
                Body = body
            };
#if (DEBUG)
            Console.Write(body);
#endif
            log.Warn(entry);
        }

        /// <summary>
        /// Write log as warn level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for recording to system.</param>
        /// <param name="exception">Exception for record to log system.</param>
        public static void Warn(this ILog log, string message, Exception exception)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);

            var entry = new ExceptionLogMessage
            {
                Body = body,
                Exception = exception
            };
#if (DEBUG)
            Console.Write(body);
#endif
            log.Warn(entry);
        }

        #endregion


        #region Error

        /// <summary>
        /// Write log as error level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message object for recording to system.</param>
        public static void Error(this ILog log, LogMessage.LogMessage message)
        {
            message.Type = LogType.Error;
            log.Message(message);
        }

        /// <summary>
        /// Write log as error level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for recording to system.</param>
        public static void Error(this ILog log, string message)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);

            var entry = new LogMessage.LogMessage
            {
                Body = body
            };
#if (DEBUG)
            Console.Write(body);
#endif
            log.Error(entry);
        }

        /// <summary>
        /// Write log as error level
        /// </summary>
        /// <param name="log">The ILog to record log information.</param>
        /// <param name="message">Message for recording to system.</param>
        /// <param name="exception">Exception for record to log system.</param>
        public static void Error(this ILog log, string message, Exception exception)
        {
            var body = string.Format("[{0}] {1}{2}", DateTime.Now.ToString("g"), message, Environment.NewLine);

            var entry = new ExceptionLogMessage
            {
                Body = body,
                Exception = exception
            };
#if (DEBUG)
            Console.Write(body);
#endif
            log.Error(entry);
        }

        #endregion
    }
}