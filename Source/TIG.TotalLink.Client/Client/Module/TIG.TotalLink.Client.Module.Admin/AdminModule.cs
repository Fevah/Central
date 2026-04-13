using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Module.Admin.Configuration;
using TIG.TotalLink.Client.Module.Admin.Mapping.Document;
using TIG.TotalLink.Client.Module.Admin.Mapping.Ribbon;
using TIG.TotalLink.Client.Module.Admin.Mapping.Scheduler;
using TIG.TotalLink.Client.Module.Admin.Provider;
using TIG.TotalLink.Client.Module.Admin.View.Backstage;
using TIG.TotalLink.Client.Module.Admin.View.Core.Scheduler;
using TIG.TotalLink.Client.Module.Admin.View.Document;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Calendar;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Debug;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Document;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Global;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Location;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Ribbon;
using TIG.TotalLink.Client.Module.Admin.View.Widget.Server;
using TIG.TotalLink.Client.Module.Admin.View.Widget.User;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Category;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Item;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Calendar;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Debug;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Location;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Ribbon;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Server;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.User;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Client.Module.Admin
{
    public class AdminModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<ClientServiceConfiguration>().As<IServiceConfiguration>().SingleInstance();
            builder.RegisterType<AdminFacade>().As<IAdminFacade>().SingleInstance();
            builder.RegisterType<WidgetProvider>().As<IWidgetProvider>().SingleInstance();
            builder.RegisterType<DataModelTypeProvider>().As<IDataModelTypeProvider>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<WidgetCardView>().InstancePerDependency();
            builder.RegisterType<WidgetCardViewModel>().InstancePerDependency();
            builder.RegisterType<ThemeGalleryView>().InstancePerDependency();
            builder.RegisterType<ThemeGalleryViewModel>().InstancePerDependency();

            builder.RegisterType<DocumentView>().InstancePerDependency();
            builder.RegisterType<DocumentViewModel>().InstancePerDependency();
            builder.RegisterType<PanelViewModel>().InstancePerDependency();
            builder.RegisterType<PanelGroupViewModel>().InstancePerDependency();

            builder.RegisterType<RibbonDefaultCategoryViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonCategoryViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonPageViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonGroupViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonButtonItemViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonSeparatorItemViewModel>().InstancePerDependency();
            
            builder.RegisterType<CountryListView>().InstancePerDependency();
            builder.RegisterType<CountryListViewModel>().InstancePerDependency();
            builder.RegisterType<StateListView>().InstancePerDependency();
            builder.RegisterType<StateListViewModel>().InstancePerDependency();
            builder.RegisterType<PostcodeListView>().InstancePerDependency();
            builder.RegisterType<PostcodeListViewModel>().InstancePerDependency();

            builder.RegisterType<DetailView>().InstancePerDependency();
            builder.RegisterType<DetailViewModel>().InstancePerDependency();
            builder.RegisterType<MessageLogView>().InstancePerDependency();
            builder.RegisterType<MessageLogViewModel>().InstancePerDependency();

            builder.RegisterType<UserListView>().InstancePerDependency();
            builder.RegisterType<UserListViewModel>().InstancePerDependency();

            builder.RegisterType<SchedulerDetailView>().InstancePerDependency();
            builder.RegisterType<SchedulerDetailViewModel>().InstancePerDependency();
            builder.RegisterType<SchedulerItemViewModel>().InstancePerDependency();

            builder.RegisterType<CalendarView>().InstancePerDependency();
            builder.RegisterType<CalendarViewModel>().InstancePerDependency();

            builder.RegisterType<DocumentListView>().InstancePerDependency();
            builder.RegisterType<DocumentListViewModel>().InstancePerDependency();
            builder.RegisterType<PanelListView>().InstancePerDependency();
            builder.RegisterType<PanelListViewModel>().InstancePerDependency();
            builder.RegisterType<DocumentImporterView>().InstancePerDependency();
            builder.RegisterType<DocumentImporterViewModel>().InstancePerDependency();
            builder.RegisterType<DocumentUploaderView>().InstancePerDependency();
            builder.RegisterType<DocumentUploaderViewModel>().InstancePerDependency();
            builder.RegisterType<WidgetListView>().InstancePerDependency();
            builder.RegisterType<WidgetListViewModel>().InstancePerDependency();
            builder.RegisterType<DocumentActionListView>().InstancePerDependency();
            builder.RegisterType<DocumentActionListViewModel>().InstancePerDependency();

            builder.RegisterType<RibbonCategoryListView>().InstancePerDependency();
            builder.RegisterType<RibbonCategoryListViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonPageListView>().InstancePerDependency();
            builder.RegisterType<RibbonPageListViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonGroupListView>().InstancePerDependency();
            builder.RegisterType<RibbonGroupListViewModel>().InstancePerDependency();
            builder.RegisterType<RibbonItemListView>().InstancePerDependency();
            builder.RegisterType<RibbonItemListViewModel>().InstancePerDependency();

            builder.RegisterType<MessageMonitorListView>().InstancePerDependency();
            builder.RegisterType<MessageMonitorListViewModel>().InstancePerDependency();

            builder.RegisterType<SequenceListView>().InstancePerDependency();
            builder.RegisterType<SequenceListViewModel>().InstancePerDependency();

            // Register Automapper profiles
            Mapper.AddProfile<PanelProfile>();
            Mapper.AddProfile<PanelGroupProfile>();

            Mapper.AddProfile<RibbonCategoryProfile>();
            Mapper.AddProfile<RibbonPageProfile>();
            Mapper.AddProfile<RibbonGroupProfile>();
            Mapper.AddProfile<RibbonItemProfile>();

            Mapper.AddProfile<AppointmentProfile>();
        }

        #endregion
    }
}
