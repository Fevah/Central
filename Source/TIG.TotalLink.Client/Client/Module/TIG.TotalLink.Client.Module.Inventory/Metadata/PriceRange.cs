using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{
    [FacadeType(typeof(IInventoryFacade))]
    [DisplayField("MinimumQuantity")]
    public partial class PriceRange
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PriceRange> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.MinimumQuantity)
                .ContainsProperty(p => p.DirectUnitCost)
                .ContainsProperty(p => p.UnitPrice);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.MinimumQuantity)
                    .ContainsProperty(p => p.DirectUnitCost)
                    .ContainsProperty(p => p.UnitPrice);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PriceRange> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.MinimumQuantity);

            builder.Property(p => p.DirectUnitCost)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.UnitPrice)
                .ReplaceEditor(new CurrencyEditorDefinition());
        }

        #endregion
    }
}