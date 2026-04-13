using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.Uploader
{
    public class StockAdjustmentUploaderDataModel : UploaderDataModelBase
    {
        #region Private Properties

        private DateTime _dateReceived;
        private StockAdjustmentReason _adjustmentReason;
        private Contact _vendor;
        private string _conNote;
        private string _legacyReference;
        private Style _style;
        private Colour _colour;
        private Size _size;
        private Sku _sku;
        private int _quantity;
        private WarehouseLocation _targetWarehouse;
        private BinLocation _targetBin;
        private PhysicalStockType _targetStockType;
        private WarehouseLocation _sourceWarehouse;
        private BinLocation _sourceBin;
        private PhysicalStockType _sourceStockType;
        private string _notes;

        #endregion


        #region Public Properties

        public DateTime DateReceived
        {
            get { return _dateReceived; }
            set { SetProperty(ref _dateReceived, value, () => DateReceived); }
        }

        public StockAdjustmentReason AdjustmentReason
        {
            get { return _adjustmentReason; }
            set { SetProperty(ref _adjustmentReason, value, () => AdjustmentReason); }
        }

        public Contact Vendor
        {
            get { return _vendor; }
            set { SetProperty(ref _vendor, value, () => Vendor); }
        }

        public string ConNote
        {
            get { return _conNote; }
            set { SetProperty(ref _conNote, value, () => ConNote); }
        }

        public string LegacyReference
        {
            get { return _legacyReference; }
            set { SetProperty(ref _legacyReference, value, () => LegacyReference); }
        }

        public Style Style
        {
            get { return _style; }
            set { SetProperty(ref _style, value, () => Style); }
        }

        public Colour Colour
        {
            get { return _colour; }
            set { SetProperty(ref _colour, value, () => Colour); }
        }

        public Size Size
        {
            get { return _size; }
            set { SetProperty(ref _size, value, () => Size); }
        }

        public Sku Sku
        {
            get { return _sku; }
            set { SetProperty(ref _sku, value, () => Sku); }
        }

        public int Quantity
        {
            get { return _quantity; }
            set { SetProperty(ref _quantity, value, () => Quantity); }
        }

        public WarehouseLocation TargetWarehouse
        {
            get { return _targetWarehouse; }
            set { SetProperty(ref _targetWarehouse, value, () => TargetWarehouse); }
        }

        public BinLocation TargetBin
        {
            get { return _targetBin; }
            set { SetProperty(ref _targetBin, value, () => TargetBin); }
        }

        public PhysicalStockType TargetStockType
        {
            get { return _targetStockType; }
            set { SetProperty(ref _targetStockType, value, () => TargetStockType); }
        }

        public WarehouseLocation SourceWarehouse
        {
            get { return _sourceWarehouse; }
            set { SetProperty(ref _sourceWarehouse, value, () => SourceWarehouse); }
        }

        public BinLocation SourceBin
        {
            get { return _sourceBin; }
            set { SetProperty(ref _sourceBin, value, () => SourceBin); }
        }

        public PhysicalStockType SourceStockType
        {
            get { return _sourceStockType; }
            set { SetProperty(ref _sourceStockType, value, () => SourceStockType); }
        }

        public string Notes
        {
            get { return _notes; }
            set { SetProperty(ref _notes, value, () => Notes); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<StockAdjustmentUploaderDataModel> builder)
        {
            builder.Property(p => p.DateReceived).ReadOnly();
            builder.Property(p => p.AdjustmentReason)
                .ReadOnly()
                .Required();
            builder.Property(p => p.Vendor).ReadOnly();
            builder.Property(p => p.ConNote)
                .ReadOnly()
                .MaxLength(255);
            builder.Property(p => p.LegacyReference)
                .ReadOnly()
                .MaxLength(100);
            builder.Property(p => p.Style).ReadOnly();
            builder.Property(p => p.Colour).ReadOnly();
            builder.Property(p => p.Size).ReadOnly();
            builder.Property(p => p.Sku).ReadOnly();
            builder.Property(p => p.Quantity)
                .ReadOnly()
                .Required();
            builder.Property(p => p.TargetWarehouse)
                .ReadOnly()
                .Required();
            builder.Property(p => p.TargetBin)
                .ReadOnly()
                .Required();
            builder.Property(p => p.TargetStockType)
                .ReadOnly()
                .Required();
            builder.Property(p => p.SourceWarehouse).ReadOnly();
            builder.Property(p => p.SourceBin).ReadOnly();
            builder.Property(p => p.SourceStockType).ReadOnly();
            builder.Property(p => p.Notes).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<StockAdjustmentUploaderDataModel> builder)
        {
        }

        #endregion
    }
}