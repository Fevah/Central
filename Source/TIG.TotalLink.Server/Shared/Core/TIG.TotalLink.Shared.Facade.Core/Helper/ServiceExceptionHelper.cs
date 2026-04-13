using System;
using System.Linq;
using System.ServiceModel;
using TIG.TotalLink.Shared.Contract.Core;

namespace TIG.TotalLink.Shared.Facade.Core.Helper
{
    public class ServiceExceptionHelper
    {
        #region Constructors

        public ServiceExceptionHelper(Exception exception)
        {
            // Abort if the exception is null
            if (exception == null)
                return;

            // Attempt to extract info from a FaultException
            if (ProcessFaultException(exception))
                return;

            // Attempt to extract info from an AggregateException
            if (ProcessAggregateException(exception))
                return;

            // If the exception didn't match any of the known types above, then just set the message
            Message = exception.Message;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The error message from within the exception that will most likely be useful to display to the user.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// The exception that the Message was extracted from, if the original exception matched any of the known exception types.
        /// </summary>
        public Exception FoundException { get; private set; }

        /// <summary>
        /// Indicates if the original exception matched any of the known exception types.
        /// </summary>
        public bool HasException
        {
            get { return FoundException != null; }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Processes a FaultException.
        /// </summary>
        /// <param name="exception">The exception to look for the FaultException in.</param>
        /// <returns>True if a FaultException was found; otherwise false.</returns>
        private bool ProcessFaultException(Exception exception)
        {
            // Abort if the exception is not a FaultException
            var faultException = exception as FaultException<ServiceFault>;
            if (faultException == null)
                return false;

            // Store the message and FaultException
            Message = faultException.Detail.Message;
            FoundException = faultException;

            return true;
        }

        /// <summary>
        /// Processes an AggregateException.
        /// </summary>
        /// <param name="exception">The exception to look for the AggregateException in.</param>
        /// <returns>True if a AggregateException was found; otherwise false.</returns>
        private bool ProcessAggregateException(Exception exception)
        {
            // Abort if the exception is not a AggregateException
            var aggregateException = exception as AggregateException;
            if (aggregateException == null)
                return false;

            // Store the message and FaultException
            Message = string.Join("\r\n", aggregateException.InnerExceptions.Select(i => i.Message));
            FoundException = aggregateException;

            return true;
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            if (FoundException != null)
                return FoundException.ToString();

            return Message;
        }

        #endregion
    }
}
