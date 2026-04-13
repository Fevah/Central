using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel.SalesOrder
{
    [DisplayField("Sku")]
    public class SalesOrderItemReleaseDataViewModel : LocalDataObjectBase
    {
        #region Public Enums

        public enum ReleaseStatuses
        {
            None,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/SalesOrder/ReleaseAll.png")]
            [EnumToolTip("All remaining items on this line will be released.")]
            ReleaseAll,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/SalesOrder/ReleasePart.png")]
            [EnumToolTip("Only part of the remaining items on this line will be released.")]
            ReleasePart,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/SalesOrder/OverRelease.png")]
            [EnumToolTip("You are attempting to release more items on this line than are currently available.\r\nThe release will probably fail or only some of the items will be released.")]
            OverRelease,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/SalesOrder/Released.png")]
            [EnumToolTip("There are no items remaining to be released on this line.")]
            Released,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/SalesOrder/CantReleasePart.png")]
            [EnumToolTip("You cannot release part of the items while Allow Partial Delivery is disabled.")]
            CantReleasePart
        }

        #endregion


        #region Private Fields

        private SalesOrderItem _salesOrderItem;
        private Sku _sku;
        private int _quantity;
        private int _quantityCancelled;
        private int _quantityReleased;
        private int _quantityToRelease;
        private int _availableStock;
        private decimal _costPrice;
        private decimal _sellPrice;
        private ReleaseStatuses _status;
        private bool _hasError;

        #endregion


        #region Public Properties

        /// <summary>
        /// The SalesOrderItem that this ReleaseItem represents.
        /// </summary>
        public SalesOrderItem SalesOrderItem
        {
            get { return _salesOrderItem; }
            set { SetProperty(ref _salesOrderItem, value, () => SalesOrderItem); }
        }

        /// <summary>
        /// The Sku for this item.
        /// </summary>
        public Sku Sku
        {
            get { return _sku; }
            set { SetProperty(ref _sku, value, () => Sku); }
        }

        /// <summary>
        /// The quantity of items.
        /// </summary>
        public int Quantity
        {
            get { return _quantity; }
            set
            {
                SetProperty(ref _quantity, value, () => Quantity, () =>
                {
                    RaisePropertiesChanged(() => QuantityCanRelease, () => QuantityCanCancel);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity of items that have been cancelled.
        /// </summary>
        public int QuantityCancelled
        {
            get { return _quantityCancelled; }
            set
            {
                SetProperty(ref _quantityCancelled, value, () => QuantityCancelled, () =>
                {
                    RaisePropertyChanged(() => QuantityCanRelease);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity of items that have already been released.
        /// </summary>
        public int QuantityReleased
        {
            get { return _quantityReleased; }
            set
            {
                SetProperty(ref _quantityReleased, value, () => QuantityReleased, () =>
                {
                    RaisePropertiesChanged(() => QuantityCanRelease, () => QuantityCanCancel);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity of items that are to be released.
        /// </summary>
        public int QuantityToRelease
        {
            get { return _quantityToRelease; }
            set
            {
                SetProperty(ref _quantityToRelease, value, () => QuantityToRelease, () =>
                {
                    RaisePropertyChanged(() => QuantityCanCancel);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity that remains to be released.
        /// </summary>
        public int QuantityCanRelease
        {
            get { return Quantity - QuantityCancelled - QuantityReleased; }
        }

        /// <summary>
        /// The quantity that can be cancelled.
        /// </summary>
        public int QuantityCanCancel
        {
            get { return Quantity - QuantityReleased - QuantityToRelease; }
        }

        /// <summary>
        /// The quantity of the selected Sku that is available for release in the selected BinLocations and PhysicalStockTypes.
        /// </summary>
        public int AvailableStock
        {
            get { return _availableStock; }
            set
            {
                SetProperty(ref _availableStock, value, () => AvailableStock,
                    UpdateStatus
                );
            }
        }

        /// <summary>
        /// The cost price of the item.
        /// </summary>
        public decimal CostPrice
        {
            get { return _costPrice; }
            set { SetProperty(ref _costPrice, value, () => CostPrice); }
        }

        /// <summary>
        /// The sell price of the item.
        /// </summary>
        public decimal SellPrice
        {
            get { return _sellPrice; }
            set { SetProperty(ref _sellPrice, value, () => SellPrice); }
        }

        /// <summary>
        /// The status of this release item.
        /// </summary>
        public ReleaseStatuses Status
        {
            get { return _status; }
            private set
            {
                SetProperty(ref _status, value, () => Status, () =>
                    {
                        HasError = (Status == ReleaseStatuses.CantReleasePart);
                    });
            }
        }

        /// <summary>
        /// Indicates if this ReleaseItem has any errors.
        /// </summary>
        public bool HasError
        {
            get { return _hasError; }
            private set { SetProperty(ref _hasError, value, () => HasError); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Updates the status based on the quantities.
        /// </summary>
        public void UpdateStatus()
        {
            if (QuantityCanRelease == 0)
            {
                Status = ReleaseStatuses.Released;
                return;
            }

            if (QuantityToRelease == 0)
            {
                Status = ReleaseStatuses.None;
                return;
            }

            if (QuantityToRelease > AvailableStock)
            {
                Status = ReleaseStatuses.OverRelease;
                return;
            }

            if (QuantityToRelease < QuantityCanRelease)
            {
                Status = SalesOrderItem.SalesOrder.AllowPartialDelivery
                    ? ReleaseStatuses.ReleasePart
                    : ReleaseStatuses.CantReleasePart;
                return;
            }

            Status = ReleaseStatuses.ReleaseAll;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SalesOrderItemReleaseDataViewModel> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Status)
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.Quantity)
                .ContainsProperty(p => p.QuantityReleased)
                .ContainsProperty(p => p.QuantityCancelled)
                .ContainsProperty(p => p.QuantityToRelease)
                .ContainsProperty(p => p.AvailableStock)
                .ContainsProperty(p => p.CostPrice)
                .ContainsProperty(p => p.SellPrice);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Status)
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.Quantity)
                    .ContainsProperty(p => p.QuantityReleased)
                    .ContainsProperty(p => p.QuantityCancelled)
                    .ContainsProperty(p => p.QuantityToRelease)
                    .ContainsProperty(p => p.AvailableStock)
                    .ContainsProperty(p => p.CostPrice)
                    .ContainsProperty(p => p.SellPrice);

            builder.Property(p => p.SalesOrderItem).NotAutoGenerated();
            builder.Property(p => p.Quantity).ReadOnly();
            builder.Property(p => p.QuantityReleased).ReadOnly();
            builder.Property(p => p.AvailableStock).ReadOnly();
            builder.Property(p => p.Status)
                .DisplayName("")
                .ReadOnly();
            builder.Property(p => p.QuantityCanRelease).NotAutoGenerated();
            builder.Property(p => p.QuantityCanCancel).NotAutoGenerated();
            builder.Property(p => p.HasError).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SalesOrderItemReleaseDataViewModel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Sku);

            builder.Condition(p => true)
                .ContainsProperty(p => p.QuantityCanCancel)
                .InvokesInstanceMethod(p => p.QuantityCancelled, new Action<SalesOrderItemReleaseDataViewModel, SpinEditorDefinition>((context, editor) =>
                    editor.MaxValue = context.QuantityCanCancel
                ));

            builder.Condition(p => true)
                .ContainsProperty(p => p.QuantityCanRelease)
                .InvokesInstanceMethod(p => p.QuantityToRelease, new Action<SalesOrderItemReleaseDataViewModel, SpinEditorDefinition>((context, editor) =>
                    editor.MaxValue = context.QuantityCanRelease
                ));

            builder.Property(p => p.Status)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReleaseStatuses))
                {
                    AllowPopup = false,
                    DisplayMode = EnumEditorDefinitionBase.DisplayModes.ImageOnly
                })
                .FixedWidth()
                .ColumnWidth(50);
            builder.Property(p => p.Sku)
                .ColumnWidth(300);
            builder.Property(p => p.Quantity)
                .ColumnWidth(130);
            builder.Property(p => p.QuantityReleased)
                .ColumnWidth(130);
            builder.Property(p => p.QuantityCancelled)
                .ColumnWidth(130)
                .GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.QuantityToRelease)
                .ColumnWidth(130)
                .GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.AvailableStock)
                .ColumnWidth(130);

            builder.Property(p => p.CostPrice).ReplaceEditor(new CurrencyEditorDefinition());
            builder.Property(p => p.SellPrice).ReplaceEditor(new CurrencyEditorDefinition());
        }

        #endregion
    }
}
