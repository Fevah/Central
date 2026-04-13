using System;
using System.Threading;
using Quartz;

namespace TIG.IntegrationServer.TimeoutManager.Quartz.Job
{
    internal class RunActionJob : IJob
    {
        #region Private Fields

        private static readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();
        private static Action[] _actionHolder = new Action[8];

        #endregion


        #region Public Methods

        /// <summary>
        /// Register a action
        /// </summary>
        /// <param name="action">action to run</param>
        /// <returns></returns>
        public static int Register(Action action)
        {
            Lock.EnterWriteLock();
            try
            {
                var actionId = GetFreeIndex();
                _actionHolder[actionId] = action;
                return actionId;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Get free index in action holder
        /// </summary>
        /// <returns>Free index</returns>
        private static int GetFreeIndex()
        {
            for (var i = 0; i < _actionHolder.Length; i++)
            {
                if (_actionHolder[i] == null)
                {
                    return i;
                }
            }

            ExtendActionHolder(8);
            return _actionHolder.Length;
        }

        /// <summary>
        /// Extemd action when action holder is full
        /// </summary>
        /// <param name="extensionLength">How many space need to extension</param>
        private static void ExtendActionHolder(int extensionLength)
        {
            var newActionsHolder = new Action[_actionHolder.Length + extensionLength];
            for (var i = 0; i < _actionHolder.Length; i++)
            {
                newActionsHolder[i] = _actionHolder[i];
            }
            _actionHolder = newActionsHolder;
        }

        #endregion


        #region IJob Members

        /// <summary>
        /// Execute job
        /// </summary>
        /// <param name="context"></param>
        public void Execute(IJobExecutionContext context)
        {
            var jobData = context.JobDetail.JobDataMap;
            var actionId = jobData.GetInt("actionId");

            Action action;
            Lock.EnterReadLock();
            try
            {
                action = _actionHolder[actionId];
                _actionHolder[actionId] = null;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Lock.ExitReadLock();
            }


            action.Invoke();
        }

        #endregion
    }
}
