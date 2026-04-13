using System;
using System.Collections.Generic;
using System.Reflection;
using MonitoredUndo;
using TIG.TotalLink.Client.Undo.AppContext;

namespace TIG.TotalLink.Client.Undo.Core
{
    /// <summary>
    /// An improved UndoBatch which is automatically discarded if it contains no changes, and maintains a fixed length list of change sets.
    /// </summary>
    public class UndoBatchEx : IDisposable
    {
        #region Private Fields

        private readonly UndoRoot _undoRoot;
        private static MethodInfo _onUndoStackChangedMethod;

        #endregion


        #region Constructors

        /// <summary>
        /// Starts an undo batch, which is ended when this instance is disposed. Designed for use in a using statement.
        /// </summary>
        /// <param name="instance">An object that implements ISupportsUndo. The batch will call GetUndoRoot() to get the root.</param>
        /// <param name="description">The description of this batch of changes.</param>
        /// <param name="consolidateChangesForSameInstance">Should the batch consolidate changes.</param>
        public UndoBatchEx(ISupportsUndo instance, string description, bool consolidateChangesForSameInstance)
            : this(UndoService.Current[instance.GetUndoRoot()], description, consolidateChangesForSameInstance)
        {
        }

        /// <summary>
        /// Starts an undo batch, which is ended when this instance is disposed. Designed for use in a using statement.
        /// </summary>
        /// <param name="root">The UndoRoot related to this instance.</param>
        /// <param name="description">The description of this batch of changes.</param>
        /// <param name="consolidateChangesForSameInstance">Should the batch consolidate changes.</param>
        public UndoBatchEx(UndoRoot root, string description, bool consolidateChangesForSameInstance)
        {
            if (null == root)
                return;
            _undoRoot = root;

            // If the count of undostack is larger or equals to the undo stack capacity saved in the seetings
            // Discard the first undo operation in the stack
            if (((Stack<ChangeSet>)root.UndoStack).Count >= AppUndoRootViewModel.Instance.UndoStackCapacity)
                DiscardFirstChangeSet(root);

            root.BeginChangeSetBatch(description, consolidateChangesForSameInstance);
        }

        #endregion


        #region Private Methods

        /// <summary>
        ///  Discard the first undo. (The one in the bottom of undo stack).
        /// </summary>
        /// <param name="root">The UndoRoot to inspect.</param>
        private static void DiscardFirstChangeSet(UndoRoot root)
        {
            // Get the undo stack from the UndoRoot, and get the changes from the first item in the stack
            var undoStack = (Stack<ChangeSet>)root.UndoStack;

            // Store the current undos without the first one in local
            var newStack = new Stack<ChangeSet>();
            while (undoStack.Count > 1)
            {
                newStack.Push(undoStack.Pop());
            }

            // Clear the stack and push the local undos into stack
            undoStack.Clear();
            while (newStack.Count > 0)
            {
                undoStack.Push(newStack.Pop());
            }
        }
        
        /// <summary>
        /// Discards the last change set on the undo stack if it does not contain any changes.
        /// </summary>
        /// <param name="root">The UndoRoot to inspect.</param>
        private static void DiscardLastChangeSetIfEmpty(UndoRoot root)
        {
            // Get the undo stack from the UndoRoot, and get the changes from the first item in the stack
            var undoStack = (Stack<ChangeSet>)root.UndoStack;
            if (undoStack.Count == 0)
                return;
            var changes = (IList<MonitoredUndo.Change>)undoStack.Peek().Changes;

            // If the change set is empty, remove it from the undo stack
            if (changes != null && changes.Count == 0)
            {
                undoStack.Pop();

                // Notify that the undo stack has changed
                if (_onUndoStackChangedMethod == null)
                    _onUndoStackChangedMethod = root.GetType().GetMethod("OnUndoStackChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_onUndoStackChangedMethod != null)
                    _onUndoStackChangedMethod.Invoke(root, new object[] { });
            }
        }

        #endregion


        #region IDisposable

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (null != _undoRoot)
                {
                    _undoRoot.EndChangeSetBatch();
                    DiscardLastChangeSetIfEmpty(_undoRoot);
                }
            }
        }

        /// <summary>
        /// Disposing this instance will end the associated Undo batch.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }
}
