using System;
using System.Threading.Tasks;

namespace TIG.TotalLink.Client.Core.Command
{
    /// <summary>
    /// Async command for perform command with async way.
    /// </summary>
    public class AsyncCommandEx : AsyncCommandEx<object>
    {
        #region Constructors

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method.
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        public AsyncCommandEx(Func<Task> executeMethod)
            : this(executeMethod, null, false, new bool?())
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method and use command manager
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="useCommandManager">Indicate using command manager or not</param>
        public AsyncCommandEx(Func<Task> executeMethod, bool useCommandManager)
            : this(executeMethod, null, false, useCommandManager)
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method, can execute method and use command manager.
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="canExecuteMethod">Task for can execute method.</param>
        /// <param name="useCommandManager">Indicate using command manager or not.</param>
        public AsyncCommandEx(Func<Task> executeMethod, Func<bool> canExecuteMethod, bool? useCommandManager = null)
            : this(executeMethod, canExecuteMethod, false, useCommandManager)
        {
        }

        /// <summary>
        /// Initilizes a object of the AsyncCommandEx class with execute method, can execute method, allow multiple and use command manager. 
        /// </summary>
        /// <param name="executeMethod">Task for execute method.</param>
        /// <param name="canExecuteMethod">Task for can execute method.</param>
        /// <param name="allowMultipleExecution">Indicate callow multiple execution or not.</param>
        /// <param name="useCommandManager">Indicate using command manager or not.</param>
        public AsyncCommandEx(Func<Task> executeMethod, Func<bool> canExecuteMethod, bool allowMultipleExecution, bool? useCommandManager = null)
            : base(executeMethod != null ? (o => executeMethod()) : (Func<object, Task>)null, canExecuteMethod != null ? (o => canExecuteMethod()) : (Func<object, bool>)null, allowMultipleExecution, useCommandManager)
        {
        }

        #endregion
    }
}