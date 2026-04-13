using Autofac;
using TIG.TotalLink.Client.Module.Sale.View.Widget.Delivery;
using TIG.TotalLink.Client.Module.Sale.View.Widget.Enquiry;
using TIG.TotalLink.Client.Module.Sale.View.Widget.Invoice;
using TIG.TotalLink.Client.Module.Sale.View.Widget.Quote;
using TIG.TotalLink.Client.Module.Sale.View.Widget.SalesOrder;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Enquiry;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Invoice;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale
{
    public class SaleModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<SaleFacade>().As<ISaleFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<EnquiryListView>().InstancePerDependency();
            builder.RegisterType<EnquiryListViewModel>().InstancePerDependency();
            builder.RegisterType<EnquiryItemListView>().InstancePerDependency();
            builder.RegisterType<EnquiryItemListViewModel>().InstancePerDependency();

            builder.RegisterType<QuoteListView>().InstancePerDependency();
            builder.RegisterType<QuoteListViewModel>().InstancePerDependency();
            builder.RegisterType<QuoteVersionListView>().InstancePerDependency();
            builder.RegisterType<QuoteVersionListViewModel>().InstancePerDependency();
            builder.RegisterType<QuoteItemListView>().InstancePerDependency();
            builder.RegisterType<QuoteItemListViewModel>().InstancePerDependency();
            builder.RegisterType<QuoteViewModel>().InstancePerDependency();

            builder.RegisterType<InvoiceListView>().InstancePerDependency();
            builder.RegisterType<InvoiceListViewModel>().InstancePerDependency();
            builder.RegisterType<InvoiceItemListView>().InstancePerDependency();
            builder.RegisterType<InvoiceItemListViewModel>().InstancePerDependency();

            builder.RegisterType<SalesOrderListView>().InstancePerDependency();
            builder.RegisterType<SalesOrderListViewModel>().InstancePerDependency();
            builder.RegisterType<SalesOrderItemListView>().InstancePerDependency();
            builder.RegisterType<SalesOrderItemListViewModel>().InstancePerDependency();
            builder.RegisterType<SalesOrderViewModel>().InstancePerDependency();
            builder.RegisterType<SalesOrderReleaseListView>().InstancePerDependency();
            builder.RegisterType<SalesOrderReleaseListViewModel>().InstancePerDependency();
            builder.RegisterType<SalesOrderReleaseItemListView>().InstancePerDependency();
            builder.RegisterType<SalesOrderReleaseItemListViewModel>().InstancePerDependency();

            builder.RegisterType<DeliveryListView>().InstancePerDependency();
            builder.RegisterType<DeliveryListViewModel>().InstancePerDependency();
            builder.RegisterType<DeliveryItemListView>().InstancePerDependency();
            builder.RegisterType<DeliveryItemListViewModel>().InstancePerDependency();
            builder.RegisterType<DeliveryReleaseViewModel>().InstancePerDependency();
            builder.RegisterType<PickItemListView>().InstancePerDependency();
            builder.RegisterType<PickItemListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
