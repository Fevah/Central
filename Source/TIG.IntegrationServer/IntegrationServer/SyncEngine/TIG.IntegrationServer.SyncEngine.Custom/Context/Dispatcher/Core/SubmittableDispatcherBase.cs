using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Core
{
    public abstract class SubmittableDispatcherBase : SyncEngineComponent
    {
        #region Private Fields

        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly IDictionary<object, bool> _submitters = new ConcurrentDictionary<object, bool>();

        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="log">Log writter</param>
        protected SubmittableDispatcherBase(ILog log)
            : base(log)
        {
        }

        #endregion


        #region Protected Properties

        protected bool AllSubmittersConfirmedSubmission
        {
            get
            {
                return _submitters.Values.All(i => i);
            }
        }

        #endregion


        #region Protected Methods

        protected void PerformReadOperation(Action operation)
        {
            try
            {
                _rwLock.EnterReadLock();
                operation.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("Exception during locked read operation.", ex);
                throw;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        protected void PerformWriteOperation(Action operation)
        {
            try
            {
                _rwLock.EnterWriteLock();
                operation.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("Exception during locked write operation.", ex);
                throw;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        protected void RegisterSubmitter(object submitter)
        {
            _submitters.Add(submitter, false);
        }

        protected void Submit(object submitter)
        {
            if (_submitters.ContainsKey(submitter))
            {
                _submitters[submitter] = true;
                return;
            }

            var message = string.Format("Unregistered instance tried to confirm submittion. Instance type = {0}", submitter.GetType().FullName);
            Log.Error(message);
        }

        protected void RevokeSubmittions()
        {
            foreach (var i in _submitters.Keys)
            {
                _submitters[i] = false;
            }
        }

        protected void UnregisterSubmitter(object submitter)
        {
            if (_submitters.ContainsKey(submitter))
            {
                _submitters.Remove(submitter);
                return;
            }

            var message = string.Format("Unregistered instance tried to unregister. Instance type = {0}", submitter.GetType().FullName);
            Log.Error(message);
        }

        #endregion


        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (_rwLock != null)
            {
                _rwLock.Dispose();
            }

            _disposed = true;
        }

        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        #endregion
    }
}
