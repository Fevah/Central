using System;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.Extension;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Inventory.Uploader;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class InventoryUploaderViewModel : UploaderViewModelBase<InventoryUploaderDataModel>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;
        private UnitOfWork _unitOfWork;
        private ColourCategory _colourCategory;

        #endregion


        #region Constructors

        public InventoryUploaderViewModel()
        {
        }

        public InventoryUploaderViewModel(IInventoryFacade inventoryFacade)
            : this()
        {
            _inventoryFacade = inventoryFacade;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Finds or creates a ColourCategory.
        /// </summary>
        /// <param name="name">The name of the ColourCategory to find or create.</param>
        /// <returns>A ColourCategory.</returns>
        private ColourCategory FindOrCreateColourCategory(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the ColourCategory in the database
            var colourCategory = _unitOfWork.QueryInTransaction<ColourCategory>().FirstOrDefault((d => d.Name == name));
            if (colourCategory != null)
                return colourCategory;

            // Create a new ColourCategory
            colourCategory = new ColourCategory(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return colourCategory;
        }

        /// <summary>
        /// Finds or creates a Colour.
        /// </summary>
        /// <param name="name">The name of the Colour to find or create.</param>
        /// <param name="colourCategory">The ColourCategory that the Colour belongs to.</param>
        /// <returns>A Colour.</returns>
        private Colour FindOrCreateColour(string name, ColourCategory colourCategory)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the Colour in the database
            var colour = _unitOfWork.QueryInTransaction<Colour>().FirstOrDefault((c => c.Category.Oid == colourCategory.Oid && c.Name == name));
            if (colour != null)
                return colour;

            // Create a new Colour
            colour = new Colour(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name,
                Category = colourCategory
            };
            return colour;
        }

        /// <summary>
        /// Finds or creates a BarcodeType.
        /// </summary>
        /// <param name="name">The name of the BarcodeType to find or create.</param>
        /// <returns>A BarcodeType.</returns>
        private BarcodeType FindOrCreateBarcodeType(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the BarcodeType in the database
            var barcodeType = _unitOfWork.QueryInTransaction<BarcodeType>().FirstOrDefault((b => b.Name == name));
            if (barcodeType != null)
                return barcodeType;

            // Create a new BarcodeType
            barcodeType = new BarcodeType(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return barcodeType;
        }

        /// <summary>
        /// Finds or creates a Barcode.
        /// </summary>
        /// <param name="number">The number of the Barcode to find or create.</param>
        /// <param name="barcodeType">The BarcodeType of the Barcode.</param>
        /// <returns>A Barcode.</returns>
        private Barcode FindOrCreateBarcode(string number, BarcodeType barcodeType)
        {
            // Abort if any of the parameters are empty
            if (string.IsNullOrWhiteSpace(number) || barcodeType == null)
                return null;

            // Attempt to find the Barcode in the database
            var barcode = _unitOfWork.QueryInTransaction<Barcode>().FirstOrDefault((b => b.BarcodeType.Oid == barcodeType.Oid && b.Number == number));
            if (barcode != null)
                return barcode;

            // Create a new Barcode
            barcode = new Barcode(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Number = number,
                BarcodeType = barcodeType
            };
            return barcode;
        }

        /// <summary>
        /// Finds or creates a Size.
        /// </summary>
        /// <param name="name">The name of the Size to find or create.</param>
        /// <param name="sizeRange">The SizeRange that owns this Size.</param>
        /// <returns>A Size.</returns>
        private Size FindOrCreateSize(string name, SizeRange sizeRange)
        {
            // Abort if the parameters are empty
            if (string.IsNullOrWhiteSpace(name) || sizeRange == null)
                return null;

            // Attempt to find the Size in the database
            var size = _unitOfWork.QueryInTransaction<Size>().FirstOrDefault((s => s.SizeRange.Oid == sizeRange.Oid && s.Name == name));
            if (size != null)
                return size;

            // Create a new Size
            size = new Size(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name,
                SizeRange = sizeRange
            };
            return size;
        }

        /// <summary>
        /// Finds or creates a StyleGender.
        /// </summary>
        /// <param name="name">The name of the StyleGender to find or create.</param>
        /// <returns>A StyleGender.</returns>
        private StyleGender FindOrCreateStyleGender(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the StyleGender in the database
            var styleGender = _unitOfWork.QueryInTransaction<StyleGender>().FirstOrDefault((s => s.Name == name));
            if (styleGender != null)
                return styleGender;

            // Create a new StyleGender
            styleGender = new StyleGender(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return styleGender;
        }

        /// <summary>
        /// Finds or creates a StyleCategory.
        /// </summary>
        /// <param name="name">The name of the StyleCategory to find or create.</param>
        /// <returns>A StyleCategory.</returns>
        private StyleCategory FindOrCreateStyleCategory(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the StyleCategory in the database
            var styleCategory = _unitOfWork.QueryInTransaction<StyleCategory>().FirstOrDefault((s => s.Name == name));
            if (styleCategory != null)
                return styleCategory;

            // Create a new StyleCategory
            styleCategory = new StyleCategory(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return styleCategory;
        }

        /// <summary>
        /// Finds or creates a ProductCategory.
        /// </summary>
        /// <param name="name">The name of the ProductCategory to find or create.</param>
        /// <returns>A ProductCategory.</returns>
        private ProductCategory FindOrCreateProductCategory(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the ProductCategory in the database
            var productCategory = _unitOfWork.QueryInTransaction<ProductCategory>().FirstOrDefault((p => p.Name == name));
            if (productCategory != null)
                return productCategory;

            // Create a new ProductCategory
            productCategory = new ProductCategory(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return productCategory;
        }

        /// <summary>
        /// Finds or creates a ProductType.
        /// </summary>
        /// <param name="name">The name of the ProductType to find or create.</param>
        /// <returns>A ProductType.</returns>
        private ProductType FindOrCreateProductType(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the ProductType in the database
            var productType = _unitOfWork.QueryInTransaction<ProductType>().FirstOrDefault((p => p.Name == name));
            if (productType != null)
                return productType;

            // Create a new ProductType
            productType = new ProductType(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return productType;
        }

        /// <summary>
        /// Finds or creates a StyleClass.
        /// </summary>
        /// <param name="name">The name of the StyleClass to find or create.</param>
        /// <returns>A StyleClass.</returns>
        private StyleClass FindOrCreateStyleClass(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the StyleClass in the database
            var styleClass = _unitOfWork.QueryInTransaction<StyleClass>().FirstOrDefault((s => s.Name == name));
            if (styleClass != null)
                return styleClass;

            // Create a new StyleClass
            styleClass = new StyleClass(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return styleClass;
        }

        /// <summary>
        /// Finds or creates a Fabric.
        /// </summary>
        /// <param name="name">The name of the Fabric to find or create.</param>
        /// <returns>A Fabric.</returns>
        private Fabric FindOrCreateFabric(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the Fabric in the database
            var fabric = _unitOfWork.QueryInTransaction<Fabric>().FirstOrDefault((f => f.Name == name));
            if (fabric != null)
                return fabric;

            // Create a new Fabric
            fabric = new Fabric(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return fabric;
        }

        /// <summary>
        /// Finds or creates a StyleDepartment.
        /// </summary>
        /// <param name="name">The name of the StyleDepartment to find or create.</param>
        /// <returns>A StyleDepartment.</returns>
        private StyleDepartment FindOrCreateStyleDepartment(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the StyleDepartment in the database
            var styleDepartment = _unitOfWork.QueryInTransaction<StyleDepartment>().FirstOrDefault((s => s.Name == name));
            if (styleDepartment != null)
                return styleDepartment;

            // Create a new StyleDepartment
            styleDepartment = new StyleDepartment(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return styleDepartment;
        }

        /// <summary>
        /// Finds or creates a Fit.
        /// </summary>
        /// <param name="name">The name of the Fit to find or create.</param>
        /// <returns>A Fit.</returns>
        private Fit FindOrCreateFit(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the Fit in the database
            var fit = _unitOfWork.QueryInTransaction<Fit>().FirstOrDefault((f => f.Name == name));
            if (fit != null)
                return fit;

            // Create a new Fit
            fit = new Fit(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return fit;
        }

        /// <summary>
        /// Finds or creates a SizeRange.
        /// </summary>
        /// <param name="name">The name of the SizeRange to find or create.</param>
        /// <returns>A SizeRange.</returns>
        private SizeRange FindOrCreateSizeRange(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the SizeRange in the database
            var sizeRange = _unitOfWork.QueryInTransaction<SizeRange>().FirstOrDefault((s => s.Name == name));
            if (sizeRange != null)
                return sizeRange;

            // Create a new SizeRange
            sizeRange = new SizeRange(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return sizeRange;
        }

        /// <summary>
        /// Finds or creates a BusinessDivision.
        /// </summary>
        /// <param name="name">The name of the BusinessDivision to find or create.</param>
        /// <returns>A BusinessDivision.</returns>
        private BusinessDivision FindOrCreateBusinessDivision(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the BusinessDivision in the database
            var businessDivision = _unitOfWork.QueryInTransaction<BusinessDivision>().FirstOrDefault((b => b.Name == name));
            if (businessDivision != null)
                return businessDivision;

            // Create a new BusinessDivision
            businessDivision = new BusinessDivision(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return businessDivision;
        }

        /// <summary>
        /// Finds or creates a Season.
        /// </summary>
        /// <param name="name">The name of the Season to find or create.</param>
        /// <returns>A Season.</returns>
        private Season FindOrCreateSeason(string name)
        {
            // Abort if the name is empty
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Attempt to find the Season in the database
            var season = _unitOfWork.QueryInTransaction<Season>().FirstOrDefault((s => s.Name == name));
            if (season != null)
                return season;

            // Create a new Season
            season = new Season(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Name = name
            };
            return season;
        }

        /// <summary>
        /// Creates a PriceRange.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="sku">The Sku which owns this PriceRange.</param>
        /// <returns>A PriceRange.</returns>
        private PriceRange FindOrCreatePriceRange(InventoryUploaderDataModel dataModel, Sku sku)
        {
            // Abort if the sku is null
            if (sku == null)
                return null;

            // Attempt to find the PriceRange in the database
            var priceRange = _unitOfWork.QueryInTransaction<PriceRange>().FirstOrDefault(p =>
                p.MinimumQuantity == dataModel.PriceRangeMinimumQuantity
                && p.DirectUnitCost == dataModel.PriceRangeDirectUnitCost
                && p.UnitPrice == dataModel.PriceRangeUnitPrice
                && p.Sku.Oid == sku.Oid
            );
            if (priceRange != null)
                return priceRange;

            // Create a new PriceRange
            priceRange = new PriceRange(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                MinimumQuantity = dataModel.PriceRangeMinimumQuantity,
                DirectUnitCost = dataModel.PriceRangeDirectUnitCost,
                UnitPrice = dataModel.PriceRangeUnitPrice,
                Sku = sku
            };
            return priceRange;
        }

        /// <summary>
        /// Finds or creates a Style.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="styleLookups">An object containing all the lookups for the Style.</param>
        /// <param name="webStyleLookups">An object containing all the web lookups for the Style.</param>
        /// <returns>A Style.</returns>
        private Style FindOrCreateStyle(InventoryUploaderDataModel dataModel, StyleLookupDataModel styleLookups, StyleLookupDataModel webStyleLookups)
        {
            // Abort if the code is empty
            if (string.IsNullOrWhiteSpace(dataModel.StyleCode))
                return null;

            // Attempt to find the Style in the database
            var style = _unitOfWork.QueryInTransaction<Style>().FirstOrDefault((s => s.Code == dataModel.StyleCode));
            if (style != null)
                return style;

            // Create a new Style
            style = new Style(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                Code = dataModel.StyleCode,
                Name = dataModel.StyleName,
                Gender = styleLookups.StyleGender,
                Category = styleLookups.StyleCategory,
                ProductCategory = styleLookups.ProductCategory,
                ProductType = styleLookups.ProductType,
                StyleClass = styleLookups.StyleClass,
                Fabric = styleLookups.Fabric,
                Content = dataModel.StyleContent,
                LongDescription = dataModel.StyleLongDescription,
                Department = styleLookups.StyleDepartment,
                Fit = styleLookups.Fit,
                SizeRange = styleLookups.SizeRange,
                Web_Number = dataModel.WebStyleNo,
                Web_Name = dataModel.WebStyleName,
                Web_Description = dataModel.WebStyleDescription,
                Web_LongDescription = dataModel.WebStyleExtendedDescription,
                Web_Details = dataModel.WebStyleDetails,
                Web_Category = webStyleLookups.StyleCategory,
                Web_ProductCategory = webStyleLookups.ProductCategory,
                Web_ProductType = webStyleLookups.ProductType,
                Web_StyleClass = webStyleLookups.StyleClass,
                Web_Industry = dataModel.WebStyleIndustry,
                Web_Fabric = webStyleLookups.Fabric,
                Web_Fit = webStyleLookups.Fit,
                Web_SizingTable = dataModel.WebStyleSizingTable,
                Web_SizeRange = webStyleLookups.SizeRange,
                Web_ImageCode1 = dataModel.WebStylePicture1,
                Web_ImageCode2 = dataModel.WebStylePicture2,
                Web_ImageCode3 = dataModel.WebStylePicture3
            };
            style.GenerateReferenceNumber();
            return style;
        }

        /// <summary>
        /// Finds or creates a Sku.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        /// <param name="style">The Style that the Sku belongs to.</param>
        /// <param name="colour">The Colour of the Sku.</param>
        /// <param name="size">The Size of the Sku.</param>
        /// <param name="season">The Season of the Sku.</param>
        /// <param name="barcode">The Barcode of the Sku.</param>
        /// <param name="businessDivision">The BusinessDivision of the Sku.</param>
        /// <param name="webColour">The web Colour of the Sku.</param>
        /// <param name="webSeason">The web Season of the Sku.</param>
        /// <returns>A Sku.</returns>
        private Sku FindOrCreateSku(InventoryUploaderDataModel dataModel, Style style, Colour colour, Size size, BusinessDivision businessDivision, Season season, Barcode barcode, Colour webColour, Season webSeason)
        {
            // Abort if any of the parameters are empty
            if (string.IsNullOrWhiteSpace(dataModel.Name) || style == null)
                return null;

            // Attempt to find the Sku in the database
            var sku = _unitOfWork.QueryInTransaction<Sku>().FirstOrDefault(s => s.Style.Oid == style.Oid && s.Name == dataModel.Name);
            if (sku != null)
                return sku;

            // Create a new Sku
            sku = new Sku(_unitOfWork)
            {
                Oid = Guid.NewGuid(),
                LegacyReference = dataModel.LegacyReference,
                Parent = _unitOfWork.GetDataObject(dataModel.Parent),
                Name = dataModel.Name,
                PackUnitOfMeasure = _unitOfWork.GetDataObject(dataModel.PackUnitOfMeasure),
                ItemUnitOfMeasure = _unitOfWork.GetDataObject(dataModel.ItemUnitOfMeasure),
                InventoryPostingGroup = _unitOfWork.GetDataObject(dataModel.InventoryPostingGroup),
                UnitPrice = dataModel.UnitPrice,
                CostingMethod = dataModel.CostingMethod,
                UnitCost = dataModel.UnitCost,
                GeneralProductPostingGroup = _unitOfWork.GetDataObject(dataModel.GeneralProductPostingGroup),
                Country = _unitOfWork.GetDataObject(dataModel.Country),
                GSTProductPostingGroup = _unitOfWork.GetDataObject(dataModel.GSTProductPostingGroup),
                ReplenishmentSystem = dataModel.ReplenishmentSystem,
                ReorderingPolicy = dataModel.ReorderingPolicy,
                IncludeInventory = dataModel.IncludeInventory,
                ReschedulingPeriod = dataModel.ReschedulingPeriod,
                LotAccumulationPeriod = dataModel.LotAccumulationPeriod,
                Style = style,
                Colour = colour,
                Size = size,
                BusinessDivision = businessDivision,
                Season = season,
                PriceIncludesGST = dataModel.PriceIncludesGst,
                AllowLineDiscount = dataModel.AllowLineDiscount,
                Barcode = barcode,
                Web_SkuId = dataModel.WebSkuId,
                Web_Colour = webColour,
                Web_Season = webSeason,
                Web_ImageCodeFront = dataModel.WebSkuFront,
                Web_ImageCodeBack = dataModel.WebSkuBack,
                Web_ImageCodeSide = dataModel.WebSkuSide,
                Web_ImageCodeFull = dataModel.WebSkuFull
            };
            sku.GenerateReferenceNumber();
            return sku;
        }

        #endregion


        #region Overrides

        protected override void InitializeUpload()
        {
            base.InitializeUpload();

            // Create a UnitOfWork and start notification tracking
            _unitOfWork = _inventoryFacade.CreateUnitOfWork();
            _unitOfWork.StartUiTracking(this, true, false, true, false);

            // Create required ColourCategories
            _colourCategory = FindOrCreateColourCategory("Legacy");

            // Commit the created types
            _unitOfWork.CommitChanges();
        }

        protected override void UploadRow(InventoryUploaderDataModel dataModel)
        {
            // Lookups
            var styleLookups = new StyleLookupDataModel()
            {
                StyleGender = FindOrCreateStyleGender(dataModel.StyleGenderName),
                StyleCategory = FindOrCreateStyleCategory(dataModel.StyleCategoryName),
                ProductCategory = FindOrCreateProductCategory(dataModel.ProductCategoryName),
                ProductType = FindOrCreateProductType(dataModel.ProductTypeName),
                StyleClass = FindOrCreateStyleClass(dataModel.StyleClassName),
                Fabric = FindOrCreateFabric(dataModel.FabricName),
                StyleDepartment = FindOrCreateStyleDepartment(dataModel.StyleDepartmentName),
                Fit = FindOrCreateFit(dataModel.FitName),
                SizeRange = FindOrCreateSizeRange(dataModel.SizeRangeName)
            };

            var webStyleLookups = new StyleLookupDataModel()
            {
                StyleCategory = FindOrCreateStyleCategory(dataModel.WebStyleCategory),
                ProductCategory = FindOrCreateProductCategory(dataModel.WebStyleProductCategory),
                ProductType = FindOrCreateProductType(dataModel.WebStyleProductType),
                StyleClass = FindOrCreateStyleClass(dataModel.WebStyleClass),
                Fabric = FindOrCreateFabric(dataModel.WebStyleFabric),
                Fit = FindOrCreateFit(dataModel.WebStyleFit),
                SizeRange = FindOrCreateSizeRange(dataModel.WebStyleSizeRange)
            };

            var barcodeType = FindOrCreateBarcodeType(dataModel.BarcodeType);
            var businessDivision = FindOrCreateBusinessDivision(dataModel.BusinessDivisionName);
            var season = FindOrCreateSeason(dataModel.SeasonName);
            var webSeason = FindOrCreateSeason(dataModel.WebSkuSeason);

            // Style
            var style = FindOrCreateStyle(dataModel, styleLookups, webStyleLookups);

            // Size
            var size = FindOrCreateSize(dataModel.SizeName, styleLookups.SizeRange);

            // Colour
            var colour = FindOrCreateColour(dataModel.ColourName, _colourCategory);
            var webColour = FindOrCreateColour(dataModel.WebSkuColour, _colourCategory);

            // Barcode
            var barcode = FindOrCreateBarcode(dataModel.BarcodeNumber, barcodeType);

            // Sku
            var sku = FindOrCreateSku(dataModel, style, colour, size, businessDivision, season, barcode, webColour, webSeason);

            // Price Range
            var priceRange = FindOrCreatePriceRange(dataModel, sku);
        }

        protected override void WriteBatch()
        {
            base.WriteBatch();

            // Commit the UnitOfWork
            _unitOfWork.CommitChanges();
        }

        protected override void FinalizeUpload()
        {
            base.FinalizeUpload();

            // Dispose the UnitOfWork
            try
            {
                _unitOfWork.Dispose();
            }
            catch (Exception)
            {
                // Ignore dispose exceptions
            }
        }

        #endregion
    }
}