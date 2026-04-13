using DevExpress.Data.Filtering;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{
    [FacadeType(typeof(IInventoryFacade))]
    [DisplayField("Name")]
    [EntityFilter(typeof(Sku), "Parent.Oid IN (?)", "Parent IN (?)")]
    [EntityFilter(typeof(Style), "Style.Oid IN (?)", "Style IN (?)")]
    public partial class Sku
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Sku> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Reference)
                .ContainsProperty(p => p.LegacyReference)
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Parent)
                .ContainsProperty(p => p.Style)
                .ContainsProperty(p => p.Colour)
                .ContainsProperty(p => p.Size)
                .ContainsProperty(p => p.Barcode)
                .ContainsProperty(p => p.UnitPrice)
                .ContainsProperty(p => p.UnitCost)
                .ContainsProperty(p => p.ItemUnitOfMeasure)
                .ContainsProperty(p => p.PackUnitOfMeasure)
                .ContainsProperty(p => p.CostingMethod)
                .ContainsProperty(p => p.PriceRanges)
                .ContainsProperty(p => p.Children)
                .ContainsProperty(p => p.PhysicalStocks)
                .ContainsProperty(p => p.PhysicalStock)
                .ContainsProperty(p => p.CommittedStock)
                .ContainsProperty(p => p.StockOnHand)
                .ContainsProperty(p => p.AvailableStock);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Reference)
                    .ContainsProperty(p => p.LegacyReference)
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.Parent)
                    .ContainsProperty(p => p.Style)
                    .ContainsProperty(p => p.Colour)
                    .ContainsProperty(p => p.Size)
                    .ContainsProperty(p => p.Barcode)
                    .ContainsProperty(p => p.UnitPrice)
                    .ContainsProperty(p => p.UnitCost)
                    .ContainsProperty(p => p.ItemUnitOfMeasure)
                    .ContainsProperty(p => p.PackUnitOfMeasure)
                    .ContainsProperty(p => p.CostingMethod)
                    .ContainsProperty(p => p.PhysicalStock)
                .EndGroup()
                .Group("Additional")
                    .ContainsProperty(p => p.Country)
                    .ContainsProperty(p => p.BusinessDivision)
                    .ContainsProperty(p => p.Season)
                    .ContainsProperty(p => p.GeneralProductPostingGroup)
                    .ContainsProperty(p => p.GSTProductPostingGroup)
                    .ContainsProperty(p => p.InventoryPostingGroup)
                    .ContainsProperty(p => p.ReplenishmentSystem)
                    .ContainsProperty(p => p.ReorderingPolicy)
                    .ContainsProperty(p => p.IncludeInventory)
                    .ContainsProperty(p => p.ReschedulingPeriod)
                    .ContainsProperty(p => p.LotAccumulationPeriod)
                    .ContainsProperty(p => p.AllowLineDiscount)
                    .ContainsProperty(p => p.PriceIncludesGST)
                .EndGroup()
                .Group("Stock Levels")
                    .ContainsProperty(p => p.PhysicalStock)
                    .ContainsProperty(p => p.CommittedStock)
                    .ContainsProperty(p => p.StockOnHand)
                    .ContainsProperty(p => p.AvailableStock)
                .EndGroup()
                .Group("Physical Stock")
                    .ContainsProperty(p => p.PhysicalStocks)
                .EndGroup()
                .Group("Price Ranges")
                    .ContainsProperty(p => p.PriceRanges)
                .EndGroup()
                .Group("Children")
                    .ContainsProperty(p => p.Children)
                .EndGroup()
                .Group("Web")
                    .ContainsProperty(p => p.Web_SkuId)
                    .ContainsProperty(p => p.Web_Season)
                    .ContainsProperty(p => p.Web_Colour)
                    .ContainsProperty(p => p.Web_ImageCodeFront)
                    .ContainsProperty(p => p.Web_ImageCodeBack)
                    .ContainsProperty(p => p.Web_ImageCodeSide)
                    .ContainsProperty(p => p.Web_ImageCodeFull);

            builder.Property(p => p.Reference).ReadOnly();
            builder.Property(p => p.PhysicalStock).ReadOnly();
            builder.Property(p => p.PhysicalStocks)
                .DisplayName("Stock")
                .AutoGenerated();
            builder.Property(p => p.PriceRanges).AutoGenerated();
            builder.Property(p => p.Children).AutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Sku> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.GridBaseColumnEditors()
                .Property(p => p.ReplenishmentSystem).Hidden().EndProperty()
                .Property(p => p.ReorderingPolicy).Hidden().EndProperty()
                .Property(p => p.IncludeInventory).Hidden().EndProperty()
                .Property(p => p.ReschedulingPeriod).Hidden().EndProperty()
                .Property(p => p.LotAccumulationPeriod).Hidden().EndProperty()
                .Property(p => p.GeneralProductPostingGroup).Hidden().EndProperty()
                .Property(p => p.GSTProductPostingGroup).Hidden().EndProperty()
                .Property(p => p.InventoryPostingGroup).Hidden().EndProperty()
                .Property(p => p.BusinessDivision).Hidden().EndProperty()
                .Property(p => p.Season).Hidden().EndProperty()
                .Property(p => p.AllowLineDiscount).Hidden().EndProperty()
                .Property(p => p.PriceIncludesGST).Hidden().EndProperty()
                .Property(p => p.Country).Hidden().EndProperty()
                .Property(p => p.Web_SkuId).Hidden().EndProperty()
                .Property(p => p.Web_Season).Hidden().EndProperty()
                .Property(p => p.Web_Colour).Hidden().EndProperty()
                .Property(p => p.Web_ImageCodeFront).Hidden().EndProperty()
                .Property(p => p.Web_ImageCodeBack).Hidden().EndProperty()
                .Property(p => p.Web_ImageCodeSide).Hidden().EndProperty()
                .Property(p => p.Web_ImageCodeFull).Hidden().EndProperty();

            builder.DataFormEditors()
                .Property(p => p.Web_Season).DisplayName("Season").EndProperty()
                .Property(p => p.Web_Colour).DisplayName("Colour").EndProperty()
                .Property(p => p.Web_ImageCodeFront).DisplayName("Image Code Front").EndProperty()
                .Property(p => p.Web_ImageCodeBack).DisplayName("Image Code Back").EndProperty()
                .Property(p => p.Web_ImageCodeSide).DisplayName("Image Code Side").EndProperty()
                .Property(p => p.Web_ImageCodeFull).DisplayName("Image Code Full").EndProperty();

            builder.Property(p => p.Reference).GetEditor<SpinEditorDefinition>().DisplayFormat = "D0";

            builder.Property(p => p.UnitPrice)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.CostingMethod)
                .ReplaceEditor(new ComboEditorDefinition(typeof(CostingMethod)))
                .AllowNull();

            builder.Property(p => p.UnitCost)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.ReplenishmentSystem)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReplenishmentSystem)))
                .AllowNull();

            builder.Property(p => p.ReorderingPolicy)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReorderingPolicy)))
                .AllowNull();

            builder.Property(p => p.Name).ColumnWidth(300);
            builder.Property(p => p.PhysicalStock).ColumnWidth(130);
            builder.Property(p => p.CommittedStock).ColumnWidth(130);
            builder.Property(p => p.StockOnHand).ColumnWidth(130);
            builder.Property(p => p.AvailableStock).ColumnWidth(130);

            builder.GridBaseColumnEditors().Property(p => p.PhysicalStocks)
                .ReplaceEditor(new PopupGridEditorDefinition
                {
                    EntityType = typeof(PhysicalStock),
                    FilterMethod = context => CriteriaOperator.Parse("Sku.Oid = ?", ((Sku)context).Oid)
                });

            builder.DataFormEditors().Property(p => p.PhysicalStocks)
                .HideLabel()
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(PhysicalStock),
                    FilterMethod = context => CriteriaOperator.Parse("Sku.Oid = ?", ((Sku)context).Oid)
                });

            builder.GridBaseColumnEditors().Property(p => p.PriceRanges)
                .ReplaceEditor(new PopupGridEditorDefinition
                {
                    EntityType = typeof(PriceRange),
                    FilterMethod = context => CriteriaOperator.Parse("Sku.Oid = ?", ((Sku)context).Oid)
                });

            builder.DataFormEditors().Property(p => p.PriceRanges)
                .HideLabel()
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(PriceRange),
                    FilterMethod = context => CriteriaOperator.Parse("Sku.Oid = ?", ((Sku)context).Oid)
                });

            builder.GridBaseColumnEditors().Property(p => p.Children)
                .ReplaceEditor(new PopupGridEditorDefinition
                {
                    EntityType = typeof(Sku),
                    FilterMethod = context => CriteriaOperator.Parse("Parent.Oid = ?", ((Sku)context).Oid)
                });

            builder.DataFormEditors().Property(p => p.Children)
                .HideLabel()
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(Sku),
                    FilterMethod = context => CriteriaOperator.Parse("Parent.Oid = ?", ((Sku)context).Oid)
                });
        }

        #endregion
    }
}