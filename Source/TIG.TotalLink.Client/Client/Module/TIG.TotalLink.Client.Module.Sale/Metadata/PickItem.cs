using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Shared.DataModel.Sale
{
    [FacadeType(typeof(ISaleFacade))]
    [DisplayField("DeliveryItem")]
    [EntityFilter(typeof(Delivery), "DeliveryItem.Delivery.Oid IN (?)", "DeliveryItem.Delivery IN (?)")]
    [EntityFilter(typeof(DeliveryItem), "DeliveryItem.Oid IN (?)", "DeliveryItem IN (?)")]
    [EntityFilter(typeof(WarehouseLocation), "BinLocation.WarehouseLocation.Oid IN (?)", "BinLocation.WarehouseLocation IN (?)")]
    [EntityFilter(typeof(SalesOrderRelease), "DeliveryItem.Delivery.SalesOrderRelease.Oid IN (?)", "DeliveryItem.Delivery.SalesOrderRelease IN (?)")]
    public partial class PickItem
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PickItem> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.SalesOrderRelease)
                .ContainsProperty(p => p.Delivery)
                .ContainsProperty(p => p.DeliveryItem)
                .ContainsProperty(p => p.BinLocation)
                .ContainsProperty(p => p.PhysicalStockType)
                .ContainsProperty(p => p.Quantity)
                .ContainsProperty(p => p.QuantityPicked)
                .ContainsProperty(p => p.CanBePicked)
                .ContainsProperty(p => p.CanBeDispatched);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.SalesOrderRelease)
                    .ContainsProperty(p => p.Delivery)
                    .ContainsProperty(p => p.DeliveryItem)
                    .ContainsProperty(p => p.BinLocation)
                    .ContainsProperty(p => p.PhysicalStockType)
                    .ContainsProperty(p => p.Quantity)
                    .ContainsProperty(p => p.QuantityPicked)
                    .ContainsProperty(p => p.CanBePicked)
                    .ContainsProperty(p => p.CanBeDispatched);

            builder.Property(p => p.SalesOrderRelease).ReadOnly();
            builder.Property(p => p.Delivery).ReadOnly();
            builder.Property(p => p.DeliveryItem).ReadOnly();
            builder.Property(p => p.BinLocation).ReadOnly();
            builder.Property(p => p.PhysicalStockType).ReadOnly();
            builder.Property(p => p.Quantity).ReadOnly();
            builder.Property(p => p.QuantityPicked).ReadOnly();
            builder.Property(p => p.CanBePicked).ReadOnly();
            builder.Property(p => p.CanBeDispatched).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PickItem> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.DeliveryItem)
                .ContainsProperty(p => p.BinLocation)
                .ContainsProperty(p => p.PhysicalStockType);

            builder.Property(p => p.Quantity).ColumnWidth(130);
            builder.Property(p => p.QuantityPicked).ColumnWidth(130);
            builder.Property(p => p.CanBePicked).ColumnWidth(110);
            builder.Property(p => p.CanBeDispatched).ColumnWidth(110);
        }

        #endregion
    }
}