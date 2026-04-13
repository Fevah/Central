using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Purchasing;

namespace TIG.TotalLink.Shared.DataModel.Purchasing
{
    [FacadeType(typeof(IPurchasingFacade))]
    [DisplayField("Quantity")]
    public partial class PurchaseReceiptItem
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PurchaseReceiptItem> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.PurchaseReceipt)
                .ContainsProperty(p => p.PurchaseOrderItem)
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.QuantityReceived)
                .ContainsProperty(p => p.QuantityPassedQA)
                .ContainsProperty(p => p.QuantityFailedQA);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.PurchaseReceipt)
                    .ContainsProperty(p => p.PurchaseOrderItem)
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.QuantityReceived)
                    .ContainsProperty(p => p.QuantityPassedQA)
                    .ContainsProperty(p => p.QuantityFailedQA);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PurchaseReceiptItem> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.PurchaseReceipt)
                .ContainsProperty(p => p.Sku);
        }

        #endregion
    }
}