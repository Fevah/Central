using System;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface
{
    public interface ICancellationDispatcher : IDisposable
    {
        /// <summary>
        /// CancellationRequested indicate system submitted cancellation 
        /// </summary>
        bool CancellationRequested { get; }

        /// <summary>
        /// Request cancellation
        /// </summary>
        void RequestCancellation();

        /// <summary>
        /// Wait for cancellation submission
        /// </summary>
        void WaitForCancellationSubmission();

        /// <summary>
        /// Register cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter to be register cancellation</param>
        bool RegisterCancellationSubmitter(object submitter);

        /// <summary>
        /// Submit cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter was registered cancellation</param>
        void SubmitCancellation(object submitter);

        /// <summary>
        /// Unregister cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter to be unregister cancellation</param>
        void UnregisterCancellationSubmitter(object submitter);
    }

    public static class CancellationWaitHandleInterfaceExtensions
    {
        /// <summary>
        /// Request cancellation and waitForSubmission
        /// </summary>
        /// <param name="cancellationDispatcher">cancellation dispatacher</param>
        public static void RequestCancellationAndWaitForSubmission(this ICancellationDispatcher cancellationDispatcher)
        {
            cancellationDispatcher.RequestCancellation();
            cancellationDispatcher.WaitForCancellationSubmission();
        }
    }
}
