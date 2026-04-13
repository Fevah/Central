using Autofac;
using TIG.TotalLink.Client.Module.Test.Provider;
using TIG.TotalLink.Client.Module.Test.View.Widget;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Test;

namespace TIG.TotalLink.Client.Module.Test
{
    public class TestModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<TestFacade>().As<ITestFacade>().SingleInstance();
            builder.RegisterType<TestViewModelProvider>().As<ITestViewModelProvider>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<MessageLogTestView>().InstancePerDependency();
            builder.RegisterType<MessageLogTestViewModel>().InstancePerDependency();
            builder.RegisterType<TestObjectListView>().InstancePerDependency();
            builder.RegisterType<TestObjectListViewModel>().InstancePerDependency();
            builder.RegisterType<TestViewModelListView>().InstancePerDependency();
            builder.RegisterType<TestViewModelListViewModel>().InstancePerDependency();

            builder.RegisterType<TestObjectImporterView>().InstancePerDependency();
            builder.RegisterType<TestObjectImporterViewModel>().InstancePerDependency();
            builder.RegisterType<TestObjectUploaderView>().InstancePerDependency();
            builder.RegisterType<TestObjectUploaderViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
