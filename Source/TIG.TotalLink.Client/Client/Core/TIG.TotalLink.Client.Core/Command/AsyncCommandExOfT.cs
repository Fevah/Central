using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Core.Command
{
    /// <summary>
    /// Async command for perform command with async way.
    /// </summary>
    /// <typeparam name="T">Execute parameter type.</typeparam>
    public class AsyncCommandEx<T> : AsyncCommand<T>
    {
        #region Private Fields

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isExecuting;
        private bool _shouldCancel;

        #endregion


        #region Constructors

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method.
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        public AsyncCommandEx(Func<T, Task> executeMethod)
            : base(executeMethod)
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method and use command manager.
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="useCommandManager">Indicate using command manager or not</param>
        public AsyncCommandEx(Func<T, Task> executeMethod, bool useCommandManager)
            : base(executeMethod, useCommandManager)
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method, can execute method and use command manager.
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="canExecuteMethod">Task for can execute method.</param>
        /// <param name="useCommandManager">Indicate using command manager or not.</param>
        public AsyncCommandEx(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod,
            bool? useCommandManager = null)
            : base(executeMethod, canExecuteMethod, useCommandManager)
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method, can execute method, allow multiple and use command manager. 
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="canExecuteMethod">Task for can execute method.</param>
        /// <param name="allowMultipleExecution">Indicate callow multiple execution or not.</param>
        /// <param name="useCommandManager">Indicate using command manager or not.</param>
        public AsyncCommandEx(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod, bool allowMultipleExecution,
            bool? useCommandManager = null)
            : base(executeMethod, canExecuteMethod, allowMultipleExecution, useCommandManager)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicate Command status is executing or not.
        /// </summary>
        public new bool IsExecuting
        {
            get { return _isExecuting; }
            private set
            {
                if (_isExecuting == value)
                    return;
                _isExecuting = value;
                RaisePropertyChanged(BindableBase.GetPropertyName(() => IsExecuting));
                CancelCommand.RaiseCanExecuteChanged();
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Cancellation token source for cancellation current executing task.
        /// </summary>
        public new CancellationTokenSource CancellationTokenSource
        {
            get { return _cancellationTokenSource; }
            private set
            {
                if (_cancellationTokenSource == value)
                    return;
                _cancellationTokenSource = value;
                RaisePropertyChanged(BindableBase.GetPropertyName(() => CancellationTokenSource));
            }
        }

        /// <summary>
        /// Indicator command need to be cancel or not.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public new bool ShouldCancel
        {
            get { return _shouldCancel; }
            private set
            {
                if (_shouldCancel == value)
                    return;
                _shouldCancel = value;
                RaisePropertyChanged(BindableBase.GetPropertyName(() => ShouldCancel));
            }
        }

        #endregion


        #region Public Methods
        
        /// <summary>
        /// Command execute method.
        /// </summary>
        /// <param name="parameter">Parameter for when invoke execute task.</param>
        public override void Execute(T parameter)
        {
            if (!CanExecute(parameter) || executeMethod == null)
                return;
            IsExecuting = true;
            var dispatcher = Dispatcher.CurrentDispatcher;
            CancellationTokenSource = new CancellationTokenSource();
            executeMethod(parameter).ContinueWith(t =>
            {
                // Switch to main thearding after executing task finished.
                // Throw exception from executing task, if there are any exception from executing task.
                dispatcher.BeginInvoke((Action)(() =>
                {
                    IsExecuting = false;
                    ShouldCancel = false;
                    if (!t.IsFaulted)
                        return;

                    if (t.Exception != null)
                    {
                        t.Exception.Handle(ex => true);

                        var baseException = t.Exception.GetBaseException();
                        throw new Exception(baseException.Message, baseException);
                    }
                }));
            });
        }

        #endregion
    }
}