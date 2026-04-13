using System;
using System.Data.Common;
using System.Runtime.Serialization;
using TIG.TotalLink.Shared.Contract.Core.Exception;

namespace TIG.TotalLink.Shared.Contract.Core
{
    [DataContract]
    public class ServiceFault
    {
        #region Constructors

        public ServiceFault()
        {
        }

        public ServiceFault(string message)
        {
            Message = message;
        }

        public ServiceFault(string message, System.Exception exception)
            : this(message)
        {
            // Store the full stack trace
            StackTrace = exception.StackTrace;

            // Attempt to extract info from a ServiceMethodException
            if (ProcessServiceMethodExceptionRecursive(exception))
                return;

            // Attempt to extract info from a DbException
            if (ProcessDbExceptionRecursive(exception))
                return;

            // If the exception didn't match any of the known types above, then just use the message that was passed in
            Message = message;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A message describing the fault that occurred.
        /// This message will be extracted from the InnerException that contains the error message most likely to be useful to the user.
        /// </summary>
        [DataMember]
        public string Message { get; set; }

        /// <summary>
        /// The full stack trace for the exception.
        /// </summary>
        [DataMember]
        public string StackTrace { get; set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Processes a ServiceMethodException.
        /// </summary>
        /// <param name="exception">The exception to look for the ServiceMethodException in.</param>
        /// <returns>True if a ServiceMethodException was found; otherwise false.</returns>
        private bool ProcessServiceMethodExceptionRecursive(System.Exception exception)
        {
            while (true)
            {
                // If the exception is a ServiceMethodException, extract the useful message
                var serviceMethodException = exception as ServiceMethodException;
                if (serviceMethodException != null)
                {
                    Message = serviceMethodException.Message;
                    return true;
                }

                // Check the InnerException for a ServiceMethodException
                if (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                    continue;
                }

                // No ServiceMethodException was found
                return false;
            }
        }

        /// <summary>
        /// Processes a DbException.
        /// </summary>
        /// <param name="exception">The exception to look for the DbException in.</param>
        /// <returns>True if a DbException was found; otherwise false.</returns>
        private bool ProcessDbExceptionRecursive(System.Exception exception)
        {
            while (true)
            {
                // If the exception is a DbException, extract the useful message
                var dbException = exception as DbException;
                if (dbException != null)
                {
                    Message = dbException.Message;
                    return true;
                }

                // Check the InnerException for a DbException
                if (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                    continue;
                }

                // No DbException was found
                return false;
            }
        }

        #endregion
    }
}
