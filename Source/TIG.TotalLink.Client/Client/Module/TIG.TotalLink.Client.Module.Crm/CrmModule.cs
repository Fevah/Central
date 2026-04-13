using Autofac;
using TIG.TotalLink.Client.Module.Crm.View.Widget.Contact;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm
{
    public class CrmModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<CrmFacade>().As<ICrmFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<ContactListView>().InstancePerDependency();
            builder.RegisterType<ContactListViewModel>().InstancePerDependency();
            builder.RegisterType<PersonListView>().InstancePerDependency();
            builder.RegisterType<PersonListViewModel>().InstancePerDependency();
            builder.RegisterType<BusinessListView>().InstancePerDependency();
            builder.RegisterType<BusinessListViewModel>().InstancePerDependency();
            builder.RegisterType<ChainListView>().InstancePerDependency();
            builder.RegisterType<ChainListViewModel>().InstancePerDependency();
            builder.RegisterType<CompanyListView>().InstancePerDependency();
            builder.RegisterType<CompanyListViewModel>().InstancePerDependency();
            builder.RegisterType<BranchListView>().InstancePerDependency();
            builder.RegisterType<BranchListViewModel>().InstancePerDependency();

            builder.RegisterType<CompanyImporterView>().InstancePerDependency();
            builder.RegisterType<CompanyImporterViewModel>().InstancePerDependency();
            builder.RegisterType<CompanyUploaderView>().InstancePerDependency();
            builder.RegisterType<CompanyUploaderViewModel>().InstancePerDependency();
            builder.RegisterType<VendorImporterView>().InstancePerDependency();
            builder.RegisterType<VendorImporterViewModel>().InstancePerDependency();
            builder.RegisterType<VendorUploaderView>().InstancePerDependency();
            builder.RegisterType<VendorUploaderViewModel>().InstancePerDependency();
            builder.RegisterType<PersonImporterView>().InstancePerDependency();
            builder.RegisterType<PersonImporterViewModel>().InstancePerDependency();
            builder.RegisterType<PersonUploaderView>().InstancePerDependency();
            builder.RegisterType<PersonUploaderViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
