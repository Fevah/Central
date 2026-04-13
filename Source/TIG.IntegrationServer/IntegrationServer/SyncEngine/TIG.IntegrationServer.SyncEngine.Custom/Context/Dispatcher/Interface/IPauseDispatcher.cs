using System;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface
{
    public interface IPauseDispatcher : IDisposable
    {
        bool PauseRequested { get; }

        /// <summary>
        /// Request pause
        /// </summary>
        void RequestPause();

        /// <summary>
        /// Wait for pause submission
        /// </summary>
        void WaitForPauseSubmission();

        /// <summary>
        /// Wait until pause revoked
        /// </summary>
        void WaitUntilPauseRevoked();

        /// <summary>
        /// Revoke pause request
        /// </summary>
        void RevokePauseRequest();

        /// <summary>
        /// Register pause submitter
        /// </summary>
        /// <param name="submitter">Submitter to be register pause</param>
        void RegisterPauseSubmitter(object submitter);

        /// <summary>
        /// Submit pause
        /// </summary>
        /// <param name="submitter">Submitter was register pause</param>
        void SubmitPause(object submitter);

        /// <summary>
        /// Unregeister pause submitter
        /// </summary>
        /// <param name="submitter">Submitter to be unregister</param>
        void UnregisterPauseSubmitter(object submitter);
    }

    public static class PauseWaitHandleInterfaceExtensions
    {
        /// <summary>
        /// Request cancellation and waitForSubmission
        /// </summary>
        /// <param name="pauseDispatcher">Pause dispatacher</param>
        public static void RequestPauseAndWaitForSubmission(this IPauseDispatcher pauseDispatcher)
        {
            pauseDispatcher.RequestPause();
            pauseDispatcher.WaitForPauseSubmission();
        }

        /// <summary>
        /// Wait until pause revoked if pause requested
        /// </summary>
        /// <param name="pauseDispatcher">Pause dispatacher</param>
        public static void WaitUntilPauseRevokedIfPauseRequested(this IPauseDispatcher pauseDispatcher)
        {
            if (pauseDispatcher.PauseRequested)
            {
                pauseDispatcher.WaitUntilPauseRevoked();
            }
        }

        /// <summary>
        /// Wait until pause revoked if pause requested
        /// </summary>
        /// <param name="pauseDispatcher">Pause dispatacher</param>
        /// <param name="submitter">Submitter to be submit</param>
        public static void WaitUntilPauseRevokedIfPauseRequested(this IPauseDispatcher pauseDispatcher, object submitter)
        {
            if (pauseDispatcher.PauseRequested)
            {
                pauseDispatcher.SubmitPause(submitter);
                pauseDispatcher.WaitUntilPauseRevoked();
            }
        }

        /// <summary>
        /// Revoke pause request if pause requested
        /// </summary>
        /// <param name="pauseDispatcher">Pause dispatacher</param>
        public static void RevokePauseRequestIfPauseRequested(this IPauseDispatcher pauseDispatcher)
        {
            if (pauseDispatcher.PauseRequested)
            {
                pauseDispatcher.RevokePauseRequest();
            }
        }
    }
}
