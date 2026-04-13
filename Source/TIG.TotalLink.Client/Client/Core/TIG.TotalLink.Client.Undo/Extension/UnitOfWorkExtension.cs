using System.Collections.Generic;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Undo.Helper;

namespace TIG.TotalLink.Client.Undo.Extension
{
    public static class UnitOfWorkExtension
    {
        #region Private Fields

        private static readonly Dictionary<string, UiChangeTracker> ChangeTrackers = new Dictionary<string, UiChangeTracker>();

        #endregion


        #region Public Methods

        /// <summary>
        /// Starts UI tracking on this UnitOfWork.
        /// </summary>
        /// <param name="uow">The UnitOfWork to start change tracking on.</param>
        /// <param name="notify">Indicates if notifications will be sent when changes occur.</param>
        /// <param name="allowUndo">Indicates if changes will be recorded on the undo stack.</param>
        /// <param name="batchChanges">
        /// If true, undos and notifications will be sent immediately when changes are written.
        /// If false, undos and notifications will be batched, and sent when the UnitOfWork is committed or disposed depending on the value of <paramref name="flushBatchOnCommit"/>.
        /// </param>
        /// <param name="flushBatchOnCommit">
        /// If true, batched undos and notifications will be sent when the UnitOfWork is committed.
        /// If false, batched undos and notifications will be sent when the UnitOfWork is disposed.
        /// </param>
        public static void StartUiTracking(this UnitOfWork uow, bool notify = true, bool allowUndo = true, bool batchChanges = false, bool flushBatchOnCommit = true)
        {
            // Create a new change tracker
            var changeTracker = new UiChangeTracker(uow, notify, allowUndo, batchChanges, flushBatchOnCommit);
            ChangeTrackers.Add(uow.ToString(), changeTracker);

            // Handle events
            uow.Disposed += UnitOfWork_Disposed;
        }

        /// <summary>
        /// Starts UI tracking on this UnitOfWork.
        /// </summary>
        /// <param name="uow">The UnitOfWork to start change tracking on.</param>
        /// <param name="notificationSender">The object to use as the sender when notifications are sent.</param>
        /// <param name="notify">Indicates if notifications will be sent when changes occur.</param>
        /// <param name="allowUndo">Indicates if changes will be recorded on the undo stack.</param>
        /// <param name="batchChanges">
        /// If true, undos and notifications will be sent immediately when changes are written.
        /// If false, undos and notifications will be batched, and sent when the UnitOfWork is committed or disposed depending on the value of <paramref name="flushBatchOnCommit"/>.
        /// </param>
        /// <param name="flushBatchOnCommit">
        /// If true, batched undos and notifications will be sent when the UnitOfWork is committed.
        /// If false, batched undos and notifications will be sent when the UnitOfWork is disposed.
        /// </param>
        public static void StartUiTracking(this UnitOfWork uow, object notificationSender, bool notify = true, bool allowUndo = true, bool batchChanges = false, bool flushBatchOnCommit = true)
        {
            // Create a new change tracker
            var changeTracker = new UiChangeTracker(uow, notificationSender, notify, allowUndo, batchChanges, flushBatchOnCommit);
            ChangeTrackers.Add(uow.ToString(), changeTracker);

            // Handle events
            uow.Disposed += UnitOfWork_Disposed;
        }

        /// <summary>
        /// Stops UI tracking on this UnitOfWork.
        /// </summary>
        /// <param name="uow">The UnitOfWork to stop change tracking on.</param>
        public static void StopUiTracking(this UnitOfWork uow)
        {
            // Attempt to find an existing tracker
            UiChangeTracker changeTracker;
            if (!ChangeTrackers.TryGetValue(uow.ToString(), out changeTracker))
                return;

            // Disable all tracking
            changeTracker.AllowUndo = false;
            changeTracker.Notify = false;

            // Release the tracker
            ChangeTrackers.Remove(uow.ToString());
        }

        /// <summary>
        /// Configures this UnitOfWork to automatically dispose itself after the next commit.
        /// </summary>
        /// <param name="uow">The UnitOfWork to dispose.</param>
        public static void DisposeAfterCommit(this UnitOfWork uow)
        {
            uow.AfterCommitTransaction += UnitOfWork_AfterCommitTransaction;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the UnitOfWork.Disposed event.
        /// Active when AllowUndo = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void UnitOfWork_Disposed(object sender, System.EventArgs e)
        {
            // Disable all tracking when the UnitOfWork is disposed
            StopUiTracking((UnitOfWork)sender);
        }

        /// <summary>
        /// Handles the UnitOfWork.AfterCommitTransaction event.
        /// Used by DisposeAfterCommit.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        static void UnitOfWork_AfterCommitTransaction(object sender, SessionManipulationEventArgs e)
        {
            try
            {
                ((UnitOfWork)sender).Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
        }

        #endregion
    }
}
