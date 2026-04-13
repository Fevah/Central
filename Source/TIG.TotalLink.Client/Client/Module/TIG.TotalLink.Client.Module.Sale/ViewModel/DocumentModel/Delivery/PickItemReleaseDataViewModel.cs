using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel.Delivery
{
    public class PickItemReleaseDataViewModel : LocalDataObjectBase
    {
        #region Public Enums

        public enum ReleaseStatuses
        {
            None,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/PickAll.png")]
            [EnumToolTip("All remaining items on this line will be marked as picked.")]
            PickAll,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/PickPart.png")]
            [EnumToolTip("Only part of the remaining items on this line will be marked as picked.")]
            PickPart,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/Picked.png")]
            [EnumToolTip("There are no items remaining to be picked on this line.")]
            Picked,

            [EnumImage("pack://application:,,,/TIG.TotalLink.Client.Module.Sale;component/Image/16x16/Delivery/ShortShipped.png")]
            [EnumToolTip("Some of the remaining items on this line will become short-shipped.")]
            ShortShipped
        }

        #endregion


        #region Private Fields

        private PickItem _pickItem;
        private Shared.DataModel.Sale.Delivery _delivery;
        private DeliveryReleaseDataViewModel _deliveryData;
        private Sku _sku;
        private BinLocation _binLocation;
        private PhysicalStockType _physicalStockType;
        private int _quantity;
        private int _quantityPicked;
        private int _quantityToPick;
        private ReleaseStatuses _status;

        #endregion


        #region Public Properties

        /// <summary>
        /// The PickItem that this data represents.
        /// </summary>
        public PickItem PickItem
        {
            get { return _pickItem; }
            set { SetProperty(ref _pickItem, value, () => PickItem); }
        }

        /// <summary>
        /// The Delivery that the PickItem belongs to.
        /// </summary>
        public Shared.DataModel.Sale.Delivery Delivery
        {
            get { return _delivery; }
            set { SetProperty(ref _delivery, value, () => Delivery); }
        }

        /// <summary>
        /// The DeliveryData that this data belongs to.
        /// </summary>
        public DeliveryReleaseDataViewModel DeliveryData
        {
            get { return _deliveryData; }
            set { SetProperty(ref _deliveryData, value, () => DeliveryData); }
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
        /// The BinLocation for this item.
        /// </summary>
        public BinLocation BinLocation
        {
            get { return _binLocation; }
            set { SetProperty(ref _binLocation, value, () => BinLocation); }
        }

        /// <summary>
        /// The PhysicalStockType for this item.
        /// </summary>
        public PhysicalStockType PhysicalStockType
        {
            get { return _physicalStockType; }
            set { SetProperty(ref _physicalStockType, value, () => PhysicalStockType); }
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
                    RaisePropertyChanged(() => QuantityCanPick);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity of items previously picked.
        /// </summary>
        public int QuantityPicked
        {
            get { return _quantityPicked; }
            set
            {
                SetProperty(ref _quantityPicked, value, () => QuantityPicked, () =>
                {
                    RaisePropertyChanged(() => QuantityCanPick);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity of items to mark as picked.
        /// </summary>
        public int QuantityToPick
        {
            get { return _quantityToPick; }
            set
            {
                SetProperty(ref _quantityToPick, value, () => QuantityToPick, () =>
                {
                    RaisePropertyChanged(() => QuantityShortShipped);
                    UpdateStatus();
                });
            }
        }

        /// <summary>
        /// The quantity that remains to be picked.
        /// </summary>
        public int QuantityCanPick
        {
            get { return Quantity - QuantityPicked; }
        }

        /// <summary>
        /// The quantity that will be short shipped.
        /// </summary>
        public int QuantityShortShipped
        {
            get { return (DeliveryDataStatus == DeliveryReleaseDataViewModel.ReleaseStatuses.Dispatch ? QuantityCanPick - QuantityToPick : 0); }
        }

        /// <summary>
        /// The status of the delivery data.
        /// </summary>
        public DeliveryReleaseDataViewModel.ReleaseStatuses DeliveryDataStatus
        {
            get { return _deliveryData != null ? _deliveryData.Status : DeliveryReleaseDataViewModel.ReleaseStatuses.None; }
        }

        /// <summary>
        /// The status of this release item.
        /// </summary>
        public ReleaseStatuses Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value, () => Status); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Updates the status based on the quantities.
        /// </summary>
        public void UpdateStatus()
        {
            if (QuantityCanPick == 0)
            {
                Status = ReleaseStatuses.Picked;
                return;
            }

            if (QuantityShortShipped > 0)
            {
                Status = ReleaseStatuses.ShortShipped;
                return;
            }

            if (QuantityToPick == 0)
            {
                Status = ReleaseStatuses.None;
                return;
            }

            if (QuantityToPick < QuantityCanPick)
            {
                Status = ReleaseStatuses.PickPart;
                return;
            }

            Status = ReleaseStatuses.PickAll;
        }

        /// <summary>
        /// Refreshes properties that are based on the DeliveryData.
        /// </summary>
        public void RefreshDeliveryData()
        {
            RaisePropertiesChanged(() => DeliveryDataStatus, () => QuantityShortShipped);
            UpdateStatus();
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<PickItemReleaseDataViewModel> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.DeliveryDataStatus)
                .ContainsProperty(p => p.Status)
                .ContainsProperty(p => p.Delivery)
                .ContainsProperty(p => p.Sku)
                .ContainsProperty(p => p.BinLocation)
                .ContainsProperty(p => p.PhysicalStockType)
                .ContainsProperty(p => p.Quantity)
                .ContainsProperty(p => p.QuantityPicked)
                .ContainsProperty(p => p.QuantityToPick)
                .ContainsProperty(p => p.QuantityShortShipped);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.DeliveryDataStatus)
                    .ContainsProperty(p => p.Status)
                    .ContainsProperty(p => p.Delivery)
                    .ContainsProperty(p => p.Sku)
                    .ContainsProperty(p => p.BinLocation)
                    .ContainsProperty(p => p.PhysicalStockType)
                    .ContainsProperty(p => p.Quantity)
                    .ContainsProperty(p => p.QuantityPicked)
                    .ContainsProperty(p => p.QuantityToPick)
                    .ContainsProperty(p => p.QuantityShortShipped);

            builder.Property(p => p.DeliveryDataStatus)
                .DisplayName("")
                .ReadOnly();
            builder.Property(p => p.Status)
                .DisplayName("")
                .ReadOnly();
            builder.Property(p => p.PickItem).NotAutoGenerated();
            builder.Property(p => p.Delivery).ReadOnly();
            builder.Property(p => p.DeliveryData).NotAutoGenerated();
            builder.Property(p => p.Sku).ReadOnly();
            builder.Property(p => p.BinLocation).ReadOnly();
            builder.Property(p => p.PhysicalStockType).ReadOnly();
            builder.Property(p => p.Quantity).ReadOnly();
            builder.Property(p => p.QuantityPicked).ReadOnly();
            builder.Property(p => p.QuantityCanPick).NotAutoGenerated();
            builder.Property(p => p.QuantityShortShipped).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<PickItemReleaseDataViewModel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Sku);

            builder.Group()
                .ContainsProperty(p => p.Delivery);

            builder.Condition(p => true)
                .ContainsProperty(p => p.QuantityCanPick)
                .InvokesInstanceMethod(p => p.QuantityToPick, new Action<PickItemReleaseDataViewModel, SpinEditorDefinition>((context, editor) =>
                    editor.MaxValue = context.QuantityCanPick
                ));

            builder.Property(p => p.DeliveryDataStatus)
                .ReplaceEditor(new ComboEditorDefinition(typeof(DeliveryReleaseDataViewModel.ReleaseStatuses))
                {
                    AllowPopup = false,
                    DisplayMode = EnumEditorDefinitionBase.DisplayModes.ImageOnly
                })
                .FixedWidth()
                .ColumnWidth(50);
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
            builder.Property(p => p.QuantityPicked)
                .ColumnWidth(130);
            builder.Property(p => p.QuantityToPick)
                .ColumnWidth(130)
                .GetEditor<SpinEditorDefinition>().MinValue = 0;
            builder.Property(p => p.QuantityShortShipped)
                .ColumnWidth(150);
        }

        #endregion
    }
}
