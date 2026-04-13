using Autofac;
using TIG.TotalLink.Client.Module.Inventory.View.Widget;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory
{
    public class InventoryModule : Autofac.Module
    {
        #region Overrides

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Register services that this module provides
            builder.RegisterType<InventoryFacade>().As<IInventoryFacade>().SingleInstance();

            // Register components that this module provides
            builder.RegisterType<SkuListView>().InstancePerDependency();
            builder.RegisterType<SkuListViewModel>().InstancePerDependency();
            builder.RegisterType<StyleListView>().InstancePerDependency();
            builder.RegisterType<StyleListViewModel>().InstancePerDependency();
            builder.RegisterType<PhysicalStockListView>().InstancePerDependency();
            builder.RegisterType<PhysicalStockListViewModel>().InstancePerDependency();
            builder.RegisterType<PhysicalStockTypeListView>().InstancePerDependency();
            builder.RegisterType<PhysicalStockTypeListViewModel>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentListView>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentListViewModel>().InstancePerDependency();
            builder.RegisterType<WarehouseLocationListView>().InstancePerDependency();
            builder.RegisterType<WarehouseLocationListViewModel>().InstancePerDependency();
            builder.RegisterType<BinLocationListView>().InstancePerDependency();
            builder.RegisterType<BinLocationListViewModel>().InstancePerDependency();

            builder.RegisterType<InventoryImporterView>().InstancePerDependency();
            builder.RegisterType<InventoryImporterViewModel>().InstancePerDependency();
            builder.RegisterType<InventoryUploaderView>().InstancePerDependency();
            builder.RegisterType<InventoryUploaderViewModel>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentImporterView>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentImporterViewModel>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentUploaderView>().InstancePerDependency();
            builder.RegisterType<StockAdjustmentUploaderViewModel>().InstancePerDependency();
        }

        #endregion
    }
}
