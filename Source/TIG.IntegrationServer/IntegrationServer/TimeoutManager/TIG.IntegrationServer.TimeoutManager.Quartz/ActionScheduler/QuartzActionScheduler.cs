using System;
using Quartz;
using TIG.IntegrationServer.TimeoutManager.Core.Interface;
using TIG.IntegrationServer.TimeoutManager.Quartz.Job;

namespace TIG.IntegrationServer.TimeoutManager.Quartz.ActionScheduler
{
    public class QuartzActionScheduler : IActionScheduler
    {
        #region Private Fields

        private readonly IScheduler _scheduler;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with scheduler
        /// </summary>
        /// <param name="scheduler">Scheduler provider</param>
        public QuartzActionScheduler(IScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        #endregion


        #region IActionScheduler Members

        /// <summary>
        /// Delay run task
        /// </summary>
        /// <param name="action">Action for delay to run</param>
        /// <param name="delay">How long to delay</param>
        public void InvokeActionWithDelay(Action action, TimeSpan delay)
        {
            var actionId = RunActionJob.Register(action);
            var job = JobBuilder.Create<RunActionJob>()
                .UsingJobData("actionId", actionId)
                .WithDescription("run action job")
                .Build();

            var offset = new DateTimeOffset(DateTime.UtcNow.Add(delay));
            var trigger = TriggerBuilder.Create()
                .WithDescription("run action job trigger")
                .StartAt(offset)
                //.WithSimpleSchedule()
                .Build();

            _scheduler.ScheduleJob(job, trigger);
        }

        /// <summary>
        /// Delay run task
        /// </summary>
        /// <param name="action">Action for delay to run</param>
        /// <param name="delay">How long to delay</param>
        /// <param name="callBack">CallBack when trigger this action after delay.</param>
        public void InvokeActionWithDelay(Action action, TimeSpan delay, Action callBack)
        {
            var complexAction = new Action(() =>
            {
                action.Invoke();
                callBack.Invoke();
            });
            InvokeActionWithDelay(complexAction, delay);
        }

        #endregion
    }
}
