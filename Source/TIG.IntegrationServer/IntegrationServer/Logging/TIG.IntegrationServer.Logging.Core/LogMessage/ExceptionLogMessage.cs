using System;
using System.Text;

namespace TIG.IntegrationServer.Logging.Core.LogMessage
{
    public class ExceptionLogMessage : LogMessage
    {
        #region Public Properties

        public Exception Exception { get; set; }

        #endregion


        #region Overrides

        public override string ToString()
        {
            // Build exception content on the nested
            var exceptionAsString = new StringBuilder();
            {
                var currentException = Exception;
                var currentExceptionDepth = 0;

                while (currentException != null)
                {
                    exceptionAsString.AppendFormat("Exception{0}Type={1}", currentExceptionDepth, currentException.GetType().FullName);
                    exceptionAsString.AppendLine();
                    exceptionAsString.AppendFormat("Exception{0}Message={1}", currentExceptionDepth, currentException.Message);
                    exceptionAsString.AppendLine();
                    exceptionAsString.AppendFormat("Exception{0}Stack={1}", currentExceptionDepth, currentException.StackTrace);

                    currentException = currentException.InnerException;
                    currentExceptionDepth++;
                }
            }

            return string.Format("{0}{1}{2}",
                base.ToString(),
                Environment.NewLine,
                exceptionAsString);
        }

        #endregion
    }
}
