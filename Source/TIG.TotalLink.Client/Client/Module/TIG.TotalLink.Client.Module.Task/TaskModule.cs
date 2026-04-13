using Autofac;
using TIG.TotalLink.Client.Module.Task.View.Widget.Task;
using TIG.TotalLink.Client.Module.Task.ViewModel.Widget.Task;
using TIG.TotalLink.Shared.Facade.Task;

namespace TIG.TotalLink.Client.Module.Task
{
    public class TaskModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<TaskFacade>().As<ITaskFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<TaskListView>().InstancePerDependency();
            builder.RegisterType<TaskListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
