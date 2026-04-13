using System;

namespace TIG.IntegrationServer.TimeoutManager.Core.Interface
{
    public interface IActionScheduler
    {
        /// <summary>
        /// Delay run task
        /// </summary>
        /// <param name="action">Action for delay to run</param>
        /// <param name="delay">How long to delay</param>
        void InvokeActionWithDelay(Action action, TimeSpan delay);

        /// <summary>
        /// Delay run task
        /// </summary>
        /// <param name="action">Action for delay to run</param>
        /// <param name="delay">How long to delay</param>
        /// <param name="callBack">CallBack when trigger this action after delay.</param>
        void InvokeActionWithDelay(Action action, TimeSpan delay, Action callBack);
    }
}
