using DevExpress.Data;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{
    [FacadeType(typeof(IInventoryFacade))]
    [DisplayField("Sku")]
    public partial class StockAdjustment
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<StockAdjustment> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.CreatedDate)
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.Quantity)
                .ContainsProperty(p => p.Reason)
                .ContainsProperty(p => p.TargetBinLocation)
                .ContainsProperty(p => p.TargetPhysicalStockType)
                .ContainsProperty(p => p.SourceBinLocation)
                .ContainsProperty(p => p.SourcePhysicalStockType)
                .ContainsProperty(p => p.Vendor)
                .ContainsProperty(p => p.VendorReference)
                .ContainsProperty(p => p.Notes);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.Quantity)
                    .ContainsProperty(p => p.Reason)
                    .GroupBox("Target")
                        .ContainsProperty(p => p.TargetBinLocation)
                        .ContainsProperty(p => p.TargetPhysicalStockType)
                    .EndGroup()
                    .GroupBox("Source")
                        .ContainsProperty(p => p.SourceBinLocation)
                        .ContainsProperty(p => p.SourcePhysicalStockType)
                    .EndGroup()
                    .GroupBox("Vendor")
                        .ContainsProperty(p => p.Vendor)
                        .ContainsProperty(p => p.VendorReference)
                    .EndGroup()
                .EndGroup()
                .Group("Notes")
                    .ContainsProperty(p => p.Notes);

            builder.Property(p => p.Sku).Required();
            builder.Property(p => p.Quantity).Required();
            builder.Property(p => p.Reason).Required();
            builder.Property(p => p.TargetBinLocation).Required();
            builder.Property(p => p.TargetPhysicalStockType).Required();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<StockAdjustment> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.CreatedDate, ColumnSortOrder.Descending);

            builder.Condition(s => s != null && s.Reason != null && s.Reason.IsSourceIncrease != null)
                .ContainsProperty(p => p.Reason)
                .AffectsGroupEnabled("Source")
                .AffectsPropertyEnabled(p => p.SourceBinLocation)
                .AffectsPropertyEnabled(p => p.SourcePhysicalStockType)
                .AffectsPropertyRequired(p => p.SourceBinLocation)
                .AffectsPropertyRequired(p => p.SourcePhysicalStockType);

            builder.GridBaseColumnEditors()
                .Property(p => p.CreatedDate).Visible().EndProperty();

            builder.DataFormEditors()
                .Property(p => p.TargetBinLocation).DisplayName("Bin Location").EndProperty()
                .Property(p => p.TargetPhysicalStockType).DisplayName("Stock Type").EndProperty()
                .Property(p => p.SourceBinLocation).DisplayName("Bin Location").EndProperty()
                .Property(p => p.SourcePhysicalStockType).DisplayName("Stock Type").EndProperty()
                .Property(p => p.VendorReference).DisplayName("Reference").EndProperty()
                .Property(p => p.Notes).HideLabel().EndProperty();

            builder.GridBaseColumnEditors()
                .Property(p => p.Sku).ReadOnly().EndProperty()
                .Property(p => p.Quantity).ReadOnly().EndProperty()
                .Property(p => p.Reason).ReadOnly().EndProperty()
                .Property(p => p.TargetBinLocation).ReadOnly().EndProperty()
                .Property(p => p.TargetPhysicalStockType).ReadOnly().EndProperty()
                .Property(p => p.SourceBinLocation).ReadOnly().EndProperty()
                .Property(p => p.SourcePhysicalStockType).ReadOnly().EndProperty();

            builder.Property(p => p.Quantity).GetEditor<SpinEditorDefinition>().MinValue = 1;

            builder.Property(p => p.Notes).ReplaceEditor(new MemoEditorDefinition());
        }

        /// <summary>
        /// Builds metadata for form editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        public void BuildFormMetadata(EditorMetadataBuilder<StockAdjustment> builder, DataLayoutControlEx dataLayoutControl)
        {
            if (dataLayoutControl.EditMode == DetailEditMode.Add)
            {
                builder.DataFormEditors()
                    .Property(p => p.Sku).NotReadOnly().EndProperty()
                    .Property(p => p.Quantity).NotReadOnly().EndProperty()
                    .Property(p => p.Reason).NotReadOnly().EndProperty()
                    .Property(p => p.TargetBinLocation).NotReadOnly().EndProperty()
                    .Property(p => p.TargetPhysicalStockType).NotReadOnly().EndProperty()
                    .Property(p => p.SourceBinLocation).NotReadOnly().EndProperty()
                    .Property(p => p.SourcePhysicalStockType).NotReadOnly().EndProperty();
            }
            else
            {
                builder.DataFormEditors()
                    .Property(p => p.Sku).ReadOnly().EndProperty()
                    .Property(p => p.Quantity).ReadOnly().EndProperty()
                    .Property(p => p.Reason).ReadOnly().EndProperty()
                    .Property(p => p.TargetBinLocation).ReadOnly().EndProperty()
                    .Property(p => p.TargetPhysicalStockType).ReadOnly().EndProperty()
                    .Property(p => p.SourceBinLocation).ReadOnly().EndProperty()
                    .Property(p => p.SourcePhysicalStockType).ReadOnly().EndProperty();
            }
        }

        #endregion
    }
}