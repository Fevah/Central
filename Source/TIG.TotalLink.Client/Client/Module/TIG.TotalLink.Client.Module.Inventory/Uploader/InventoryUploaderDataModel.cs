using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Inventory;
using TIG.TotalLink.Shared.DataModel.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.Uploader
{
    public class InventoryUploaderDataModel : UploaderDataModelBase
    {
        #region Private Fields

        private string _legacyReference;
        private Sku _parent;
        private string _name;
        private UnitOfMeasure _itemUnitOfMeasure;
        private UnitOfMeasure _packUnitOfMeasure;
        private PostingGroup _inventoryPostingGroup;
        private decimal _unitPrice;
        private CostingMethod _costingMethod;
        private decimal _unitCost;
        private PostingGroup _generalProductPostingGroup;
        private Country _country;
        private PostingGroup _gstProductPostingGroup;
        private ReplenishmentSystem _replenishmentSystem;
        private ReorderingPolicy _reordingPolicy;
        private bool _includeInventory;
        private string _reschedulingPeriod;
        private string _lotAccoumulationPeriod;
        private string _styleName;
        private string _styleCode;
        private string _sizeName;
        private string _colourName;
        private string _styleGenderName;
        private string _styleCategoryName;
        private string _productCategoryName;
        private string _productTypeName;
        private string _styleClassName;
        private string _fabricName;
        private string _styleDepartmentName;
        private string _fitName;
        private string _sizeRangeName;
        private string _businessDivisionName;
        private string _seasonName;
        private string _styleContent;
        private string _styleLongDescription;
        private bool _priceIncludesGst;
        private bool _allowLineDiscount;
        private int _priceRangeMinimumQuantity;
        private decimal _priceRangeDirectUnitCost;
        private decimal _priceRangeUnitPrice;
        private string _barcodeType;
        private string _barcodeNumber;
        private string _webStyleNo;
        private string _webStyleName;
        private string _webStyleDescription;
        private string _webStyleExtendedDescription;
        private string _webStyleDetails;
        private string _webStyleCategory;
        private string _webStyleProductCategory;
        private string _webStyleProductType;
        private string _webStyleClass;
        private string _webStyleIndustry;
        private string _webStyleFabric;
        private string _webStyleFit;
        private string _webStyleSizingTable;
        private string _webStyleSizeRange;
        private string _webStylePicture1;
        private string _webStylePicture2;
        private string _webStylePicture3;
        private string _webSkuColour;
        private string _webSkuSeason;
        private string _webSkuFront;
        private string _webSkuBack;
        private string _webSkuSide;
        private string _webSkuFull;
        private string _webSkuId;

        #endregion


        #region Properties

        public Sku Parent
        {
            get { return _parent; }
            set { SetProperty(ref _parent, value, () => Parent); }
        }

        public string LegacyReference
        {
            get { return _legacyReference; }
            set { SetProperty(ref _legacyReference, value, () => LegacyReference); }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        public UnitOfMeasure PackUnitOfMeasure
        {
            get { return _packUnitOfMeasure; }
            set { SetProperty(ref _packUnitOfMeasure, value, () => PackUnitOfMeasure); }
        }

        public UnitOfMeasure ItemUnitOfMeasure
        {
            get { return _itemUnitOfMeasure; }
            set { SetProperty(ref _itemUnitOfMeasure, value, () => ItemUnitOfMeasure); }
        }

        public PostingGroup InventoryPostingGroup
        {
            get { return _inventoryPostingGroup; }
            set { SetProperty(ref _inventoryPostingGroup, value, () => InventoryPostingGroup); }
        }

        public decimal UnitPrice
        {
            get { return _unitPrice; }
            set { SetProperty(ref _unitPrice, value, () => UnitPrice); }
        }

        public CostingMethod CostingMethod
        {
            get { return _costingMethod; }
            set { SetProperty(ref _costingMethod, value, () => CostingMethod); }
        }

        public decimal UnitCost
        {
            get { return _unitCost; }
            set { SetProperty(ref _unitCost, value, () => UnitCost); }
        }

        public PostingGroup GeneralProductPostingGroup
        {
            get { return _generalProductPostingGroup; }
            set { SetProperty(ref _generalProductPostingGroup, value, () => GeneralProductPostingGroup); }
        }

        public Country Country
        {
            get { return _country; }
            set { SetProperty(ref _country, value, () => Country); }
        }

        public PostingGroup GSTProductPostingGroup
        {
            get { return _gstProductPostingGroup; }
            set { SetProperty(ref _gstProductPostingGroup, value, () => GSTProductPostingGroup); }
        }

        public ReplenishmentSystem ReplenishmentSystem
        {
            get { return _replenishmentSystem; }
            set { SetProperty(ref _replenishmentSystem, value, () => ReplenishmentSystem); }
        }

        public ReorderingPolicy ReorderingPolicy
        {
            get { return _reordingPolicy; }
            set { SetProperty(ref _reordingPolicy, value, () => ReorderingPolicy); }
        }

        public bool IncludeInventory
        {
            get { return _includeInventory; }
            set { SetProperty(ref _includeInventory, value, () => IncludeInventory); }
        }

        public string ReschedulingPeriod
        {
            get { return _reschedulingPeriod; }
            set { SetProperty(ref _reschedulingPeriod, value, () => ReschedulingPeriod); }
        }

        public string LotAccumulationPeriod
        {
            get { return _lotAccoumulationPeriod; }
            set { SetProperty(ref _lotAccoumulationPeriod, value, () => LotAccumulationPeriod); }
        }

        public string StyleName
        {
            get { return _styleName; }
            set { SetProperty(ref _styleName, value, () => StyleName); }
        }

        public string StyleCode
        {
            get { return _styleCode; }
            set { SetProperty(ref _styleCode, value, () => StyleCode); }
        }

        public string SizeName
        {
            get { return _sizeName; }
            set { SetProperty(ref _sizeName, value, () => SizeName); }
        }

        public string ColourName
        {
            get { return _colourName; }
            set { SetProperty(ref _colourName, value, () => ColourName); }
        }

        public string StyleGenderName
        {
            get { return _styleGenderName; }
            set { SetProperty(ref _styleGenderName, value, () => StyleGenderName); }
        }

        public string StyleCategoryName
        {
            get { return _styleCategoryName; }
            set { SetProperty(ref _styleCategoryName, value, () => StyleCategoryName); }
        }

        public string ProductCategoryName
        {
            get { return _productCategoryName; }
            set { SetProperty(ref _productCategoryName, value, () => ProductCategoryName); }
        }

        public string ProductTypeName
        {
            get { return _productTypeName; }
            set { SetProperty(ref _productTypeName, value, () => ProductTypeName); }
        }

        public string StyleClassName
        {
            get { return _styleClassName; }
            set { SetProperty(ref _styleClassName, value, () => StyleClassName); }
        }

        public string FabricName
        {
            get { return _fabricName; }
            set { SetProperty(ref _fabricName, value, () => FabricName); }
        }

        public string StyleDepartmentName
        {
            get { return _styleDepartmentName; }
            set { SetProperty(ref _styleDepartmentName, value, () => StyleDepartmentName); }
        }

        public string FitName
        {
            get { return _fitName; }
            set { SetProperty(ref _fitName, value, () => FitName); }
        }

        public string SizeRangeName
        {
            get { return _sizeRangeName; }
            set { SetProperty(ref _sizeRangeName, value, () => SizeRangeName); }
        }

        public string BusinessDivisionName
        {
            get { return _businessDivisionName; }
            set { SetProperty(ref _businessDivisionName, value, () => BusinessDivisionName); }
        }

        public string SeasonName
        {
            get { return _seasonName; }
            set { SetProperty(ref _seasonName, value, () => SeasonName); }
        }

        public string StyleContent
        {
            get { return _styleContent; }
            set { SetProperty(ref _styleContent, value, () => StyleContent); }
        }

        public string StyleLongDescription
        {
            get { return _styleLongDescription; }
            set { SetProperty(ref _styleLongDescription, value, () => StyleLongDescription); }
        }

        public bool PriceIncludesGst
        {
            get { return _priceIncludesGst; }
            set { SetProperty(ref _priceIncludesGst, value, () => PriceIncludesGst); }
        }

        public bool AllowLineDiscount
        {
            get { return _allowLineDiscount; }
            set { SetProperty(ref _allowLineDiscount, value, () => AllowLineDiscount); }
        }

        public int PriceRangeMinimumQuantity
        {
            get { return _priceRangeMinimumQuantity; }
            set { SetProperty(ref _priceRangeMinimumQuantity, value, () => PriceRangeMinimumQuantity); }
        }

        public decimal PriceRangeDirectUnitCost
        {
            get { return _priceRangeDirectUnitCost; }
            set { SetProperty(ref _priceRangeDirectUnitCost, value, () => PriceRangeDirectUnitCost); }
        }

        public decimal PriceRangeUnitPrice
        {
            get { return _priceRangeUnitPrice; }
            set { SetProperty(ref _priceRangeUnitPrice, value, () => PriceRangeUnitPrice); }
        }

        public string BarcodeType
        {
            get { return _barcodeType; }
            set { SetProperty(ref _barcodeType, value, () => BarcodeType); }
        }

        public string BarcodeNumber
        {
            get { return _barcodeNumber; }
            set { SetProperty(ref _barcodeNumber, value, () => BarcodeNumber); }
        }

        public string WebStyleNo
        {
            get { return _webStyleNo; }
            set { SetProperty(ref _webStyleNo, value, () => WebStyleNo); }
        }

        public string WebStyleName
        {
            get { return _webStyleName; }
            set { SetProperty(ref _webStyleName, value, () => WebStyleName); }
        }

        public string WebStyleDescription
        {
            get { return _webStyleDescription; }
            set { SetProperty(ref _webStyleDescription, value, () => WebStyleDescription); }
        }

        public string WebStyleExtendedDescription
        {
            get { return _webStyleExtendedDescription; }
            set { SetProperty(ref _webStyleExtendedDescription, value, () => WebStyleExtendedDescription); }
        }

        public string WebStyleDetails
        {
            get { return _webStyleDetails; }
            set { SetProperty(ref _webStyleDetails, value, () => WebStyleDetails); }
        }

        public string WebStyleCategory
        {
            get { return _webStyleCategory; }
            set { SetProperty(ref _webStyleCategory, value, () => WebStyleCategory); }
        }

        public string WebStyleProductCategory
        {
            get { return _webStyleProductCategory; }
            set { SetProperty(ref _webStyleProductCategory, value, () => WebStyleProductCategory); }
        }

        public string WebStyleProductType
        {
            get { return _webStyleProductType; }
            set { SetProperty(ref _webStyleProductType, value, () => WebStyleProductType); }
        }

        public string WebStyleClass
        {
            get { return _webStyleClass; }
            set { SetProperty(ref _webStyleClass, value, () => WebStyleClass); }
        }

        public string WebStyleIndustry
        {
            get { return _webStyleIndustry; }
            set { SetProperty(ref _webStyleIndustry, value, () => WebStyleIndustry); }
        }

        public string WebStyleFabric
        {
            get { return _webStyleFabric; }
            set { SetProperty(ref _webStyleFabric, value, () => WebStyleFabric); }
        }

        public string WebStyleFit
        {
            get { return _webStyleFit; }
            set { SetProperty(ref _webStyleFit, value, () => WebStyleFit); }
        }

        public string WebStyleSizingTable
        {
            get { return _webStyleSizingTable; }
            set { SetProperty(ref _webStyleSizingTable, value, () => WebStyleSizingTable); }
        }

        public string WebStyleSizeRange
        {
            get { return _webStyleSizeRange; }
            set { SetProperty(ref _webStyleSizeRange, value, () => WebStyleSizeRange); }
        }

        public string WebStylePicture1
        {
            get { return _webStylePicture1; }
            set { SetProperty(ref _webStylePicture1, value, () => WebStylePicture1); }
        }

        public string WebStylePicture2
        {
            get { return _webStylePicture2; }
            set { SetProperty(ref _webStylePicture2, value, () => WebStylePicture2); }
        }

        public string WebStylePicture3
        {
            get { return _webStylePicture3; }
            set { SetProperty(ref _webStylePicture3, value, () => WebStylePicture3); }
        }

        public string WebSkuColour
        {
            get { return _webSkuColour; }
            set { SetProperty(ref _webSkuColour, value, () => WebSkuColour); }
        }

        public string WebSkuSeason
        {
            get { return _webSkuSeason; }
            set { SetProperty(ref _webSkuSeason, value, () => WebSkuSeason); }
        }

        public string WebSkuFront
        {
            get { return _webSkuFront; }
            set { SetProperty(ref _webSkuFront, value, () => WebSkuFront); }
        }

        public string WebSkuBack
        {
            get { return _webSkuBack; }
            set { SetProperty(ref _webSkuBack, value, () => WebSkuBack); }
        }

        public string WebSkuSide
        {
            get { return _webSkuSide; }
            set { SetProperty(ref _webSkuSide, value, () => WebSkuSide); }
        }

        public string WebSkuFull
        {
            get { return _webSkuFull; }
            set { SetProperty(ref _webSkuFull, value, () => WebSkuFull); }
        }

        public string WebSkuId
        {
            get { return _webSkuId; }
            set { SetProperty(ref _webSkuId, value, () => WebSkuId); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<InventoryUploaderDataModel> builder)
        {
            builder.Property(p => p.LegacyReference)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.Name)
                .Required()
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.PackUnitOfMeasure)
                .ReadOnly();
            builder.Property(p => p.ItemUnitOfMeasure)
                .ReadOnly();
            builder.Property(p => p.InventoryPostingGroup)
                .ReadOnly();
            builder.Property(p => p.UnitPrice)
                .ReadOnly();
            builder.Property(p => p.CostingMethod)
                .ReadOnly();
            builder.Property(p => p.UnitCost)
                .Required()
                .ReadOnly();
            builder.Property(p => p.GeneralProductPostingGroup)
                .ReadOnly();
            builder.Property(p => p.Country)
                .ReadOnly();
            builder.Property(p => p.GSTProductPostingGroup)
                .ReadOnly();
            builder.Property(p => p.ReplenishmentSystem)
                .ReadOnly();
            builder.Property(p => p.ReorderingPolicy)
                .ReadOnly();
            builder.Property(p => p.IncludeInventory)
                .ReadOnly();
            builder.Property(p => p.ReschedulingPeriod)
                .MaxLength(32)
                .ReadOnly();
            builder.Property(p => p.LotAccumulationPeriod)
                .MaxLength(32)
                .ReadOnly();
            builder.Property(p => p.StyleName)
                .Required()
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleCode)
                .Required()
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.SizeName)
                .Required()
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.ColourName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleGenderName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleCategoryName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.ProductCategoryName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.ProductTypeName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleClassName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.FabricName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleDepartmentName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.FitName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.SizeRangeName)
                .Required()
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.BusinessDivisionName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.SeasonName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.StyleContent)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.StyleLongDescription)
                .ReadOnly();
            builder.Property(p => p.PriceIncludesGst)
                .ReadOnly();
            builder.Property(p => p.AllowLineDiscount)
                .ReadOnly();
            builder.Property(p => p.PriceRangeMinimumQuantity)
                .ReadOnly();
            builder.Property(p => p.PriceRangeDirectUnitCost)
                .ReadOnly();
            builder.Property(p => p.PriceRangeUnitPrice)
                .ReadOnly();
            builder.Property(p => p.BarcodeType)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.BarcodeNumber)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.WebStyleNo)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.WebStyleName)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.WebStyleDescription)
                .ReadOnly();
            builder.Property(p => p.WebStyleExtendedDescription)
                .ReadOnly();
            builder.Property(p => p.WebStyleDetails)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.WebStyleCategory)
                .ReadOnly();
            builder.Property(p => p.WebStyleProductCategory)
                .ReadOnly();
            builder.Property(p => p.WebStyleProductType)
                .ReadOnly();
            builder.Property(p => p.WebStyleClass)
                .ReadOnly();
            builder.Property(p => p.WebStyleIndustry)
                .MaxLength(100)
                .ReadOnly();
            builder.Property(p => p.WebStyleFabric)
                .ReadOnly();
            builder.Property(p => p.WebStyleFit)
                .ReadOnly();
            builder.Property(p => p.WebStyleSizingTable)
                .MaxLength(150)
                .ReadOnly();
            builder.Property(p => p.WebStylePicture1)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebStylePicture2)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebStylePicture3)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebSkuColour)
                .ReadOnly();
            builder.Property(p => p.WebSkuSeason)
                .ReadOnly();
            builder.Property(p => p.WebSkuFront)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebSkuBack)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebSkuSide)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebSkuFull)
                .MaxLength(255)
                .ReadOnly();
            builder.Property(p => p.WebSkuId)
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<InventoryUploaderDataModel> builder)
        {
            builder.Property(p => p.CostingMethod)
                .ReplaceEditor(new ComboEditorDefinition(typeof(CostingMethod)))
                .AllowNull();

            builder.Property(p => p.ReplenishmentSystem)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReplenishmentSystem)))
                .AllowNull();

            builder.Property(p => p.ReorderingPolicy)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ReorderingPolicy)))
                .AllowNull();

            builder.Property(p => p.UnitPrice)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.UnitCost)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.PriceRangeDirectUnitCost)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.PriceRangeUnitPrice)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.StyleLongDescription)
                .UnlimitedLength()
                .ReplaceEditor(new MemoEditorDefinition());

            builder.Property(p => p.WebStyleDescription)
                .UnlimitedLength()
                .ReplaceEditor(new MemoEditorDefinition());

            builder.Property(p => p.WebStyleExtendedDescription)
                .UnlimitedLength()
                .ReplaceEditor(new MemoEditorDefinition());
        }

        #endregion
    }
}