using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Shared.DataModel.Sale
{
    [FacadeType(typeof(ISaleFacade))]
    [DisplayField("PhysicalStockType")]
    public partial class SalesOrderRelease_PhysicalStockType
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SalesOrderRelease_PhysicalStockType> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.PhysicalStockType);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.PhysicalStockType);

            builder.Property(p => p.SalesOrderRelease).Hidden();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SalesOrderRelease_PhysicalStockType> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.PhysicalStockType);
        }

        #endregion
    }
}