using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TIG.IntegrationServer.Helper
{
    public class ServiceExceptionHelper
    {
        #region Constructors

        public ServiceExceptionHelper(Exception exception)
        {
            // Abort if the exception is null
            if (exception == null)
                return;

            // Attempt to extract info from a ReflectionTypeLoadException
            if (ProcessReflectionTypeLoadException(exception))
                return;

            // Attempt to extract info from a FileLoadException
            if (ProcessFileLoadException(exception))
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
        /// Processes a ReflectionTypeLoadException.
        /// </summary>
        /// <param name="exception">The exception to look for the ReflectionTypeLoadException in.</param>
        /// <returns>True if a ReflectionTypeLoadException was found; otherwise false.</returns>
        private bool ProcessReflectionTypeLoadException(Exception exception)
        {
            while (true)
            {
                // Attempt to get the exception as a ReflectionTypeLoadException
                var typeLoadException = exception as ReflectionTypeLoadException;

                // If this exception is not a ReflectionTypeLoadException, attempt to process the InnerException
                if (typeLoadException == null)
                {
                    if (exception.InnerException == null)
                        return false;

                    exception = exception.InnerException;
                    continue;
                }

                // Store the message and FaultException
                Message = string.Join("\r\n", typeLoadException.LoaderExceptions.Select(l => l.Message));
                FoundException = typeLoadException;

                return true;
            }
        }

        /// <summary>
        /// Processes a AggregateException.
        /// </summary>
        /// <param name="exception">The exception to look for the AggregateException in.</param>
        /// <returns>True if a AggregateException was found; otherwise false.</returns>
        private bool ProcessAggregateException(Exception exception)
        {
            while (true)
            {
                // Attempt to get the exception as a AggregateException
                var aggregateException = exception as AggregateException;

                // If this exception is not a AggregateException, attempt to process the InnerException
                if (aggregateException == null)
                {
                    if (exception.InnerException == null)
                        return false;

                    exception = exception.InnerException;
                    continue;
                }

                // Store the message and FaultException
                Message = string.Join("\r\n", aggregateException.InnerExceptions.Select(i => i.Message));
                FoundException = aggregateException;

                return true;
            }
        }

        /// <summary>
        /// Processes a FileLoadException.
        /// </summary>
        /// <param name="exception">The exception to look for the FileLoadException in.</param>
        /// <returns>True if a FileLoadException was found; otherwise false.</returns>
        private bool ProcessFileLoadException(Exception exception)
        {
            while (true)
            {
                // Attempt to get the exception as a FileLoadException
                var fileLoadException = exception as FileLoadException;

                // If this exception is not a FileLoadException, attempt to process the InnerException
                if (fileLoadException == null)
                {
                    if (exception.InnerException == null)
                        return false;

                    exception = exception.InnerException;
                    continue;
                }

                // Store the message and FileLoadException
                Message = fileLoadException.Message;
                FoundException = fileLoadException;

                return true;
            }
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
