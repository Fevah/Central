using Autofac;
using TIG.TotalLink.Client.Module.Workflow.View.Widget;
using TIG.TotalLink.Client.Module.Workflow.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Workflow;

namespace TIG.TotalLink.Client.Module.Workflow
{
    public class WorkflowModule : Autofac.Module
    {
        #region Overrides

        /// <summary>
        /// Register services that this module provides.
        /// </summary>
        /// <param name="builder">Container builder.</param>
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<WorkflowFacade>().As<IWorkflowFacade>().SingleInstance();


            // Register components that this module provides
            builder.RegisterType<WorkflowListView>().InstancePerDependency();
            builder.RegisterType<WorkflowListViewModel>().InstancePerDependency();

            builder.RegisterType<WorkflowActivityListView>().InstancePerDependency();
            builder.RegisterType<WorkflowActivityListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
