using Autofac;
using TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseOrder;
using TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseReceipt;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseOrder;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseReceipt;
using TIG.TotalLink.Shared.Facade.Purchasing;

namespace TIG.TotalLink.Client.Module.Purchasing
{
    public class PurchasingModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<PurchasingFacade>().As<IPurchasingFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<PurchaseOrderListView>().InstancePerDependency();
            builder.RegisterType<PurchaseOrderListViewModel>().InstancePerDependency();
            builder.RegisterType<PurchaseOrderItemListView>().InstancePerDependency();
            builder.RegisterType<PurchaseOrderItemListViewModel>().InstancePerDependency();

            builder.RegisterType<PurchaseReceiptListView>().InstancePerDependency();
            builder.RegisterType<PurchaseReceiptListViewModel>().InstancePerDependency();
            builder.RegisterType<PurchaseReceiptItemListView>().InstancePerDependency();
            builder.RegisterType<PurchaseReceiptItemListViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
