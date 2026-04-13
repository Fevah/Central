using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{
    [FacadeType(typeof(IInventoryFacade))]
    [DisplayField("Quantity")]
    public partial class PhysicalStock
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PhysicalStock> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.BinLocation)
                .ContainsProperty(p => p.PhysicalStockType)
                .ContainsProperty(p => p.Quantity)
                .ContainsProperty(p => p.CommittedStock)
                .ContainsProperty(p => p.StockOnHand)
                .ContainsProperty(p => p.AvailableStock);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.BinLocation)
                    .ContainsProperty(p => p.PhysicalStockType)
                    .ContainsProperty(p => p.Quantity)
                    .ContainsProperty(p => p.CommittedStock)
                    .ContainsProperty(p => p.StockOnHand)
                    .ContainsProperty(p => p.AvailableStock);

            builder.Property(p => p.Sku).ReadOnly();
            builder.Property(p => p.BinLocation).ReadOnly();
            builder.Property(p => p.PhysicalStockType).ReadOnly();
            builder.Property(p => p.Quantity).ReadOnly();
            builder.Property(p => p.CommittedStock).ReadOnly();
            builder.Property(p => p.StockOnHand).ReadOnly();
            builder.Property(p => p.AvailableStock).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PhysicalStock> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.PhysicalStockType);

            builder.Property(p => p.Quantity).ColumnWidth(130);
            builder.Property(p => p.CommittedStock).ColumnWidth(130);
            builder.Property(p => p.StockOnHand).ColumnWidth(130);
            builder.Property(p => p.AvailableStock).ColumnWidth(130);
        }

        #endregion
    }
}