using System.ComponentModel.Composition;
using Autofac;
using Autofac.Core;
using Quartz.Impl;
using TIG.IntegrationServer.TimeoutManager.Core.Interface;
using TIG.IntegrationServer.TimeoutManager.Quartz.ActionScheduler;

namespace TIG.IntegrationServer.DI.Autofac.Module
{
    [Export("common", typeof(IModule))]
    internal class TimeoutManagerModule : global::Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder
                .Register(c =>
                {
                    var schedulerFactory = new StdSchedulerFactory();
                    var scheduler = schedulerFactory.GetScheduler();
                    scheduler.Start();
                    var fact = new QuartzActionScheduler(scheduler);
                    return fact;
                })
                .As<IActionScheduler>()
                .SingleInstance();
        }

        #endregion
    }
}
