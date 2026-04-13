using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Inventory.Uploader;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Inventory;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;
using EnumHelper = TIG.TotalLink.Client.Core.Helper.EnumHelper;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class InventoryImporterViewModel : ImporterViewModelBase<InventoryUploaderDataModel>
    {
        #region Static Fields

        private static readonly string[] ProperCaseExclusions = { @"\d+[A-Z]", @"[A-Z]+\d+", @"XS", @"XXS", @"XL", @"CM", @"PVC" };

        #endregion


        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public InventoryImporterViewModel(IInventoryFacade inventoryFacade)
            : this()
        {
            _inventoryFacade = inventoryFacade;
        }

        public InventoryImporterViewModel()
        {
            FirstHeaderCellReference = "A6";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateNamedField(p => p.LegacyReference, SpreadsheetDataType.String, "No.", "No", "Legacy Reference", "LegacyReference"),
                CreateNamedField(p => p.Parent, SpreadsheetDataType.String, ConvertToParentSku, "Parent No.", "Parent No"),
                CreateNamedField(p => p.Name, SpreadsheetDataType.String, ConvertToProperCase, "Name", "Sku Name", "SkuName"),
                CreateNamedField(p => p.ItemUnitOfMeasure, SpreadsheetDataType.String, ConvertToUnitOfMeasure, "Base Unit of Measure", "BaseUnitofMeasure"),
                CreateNamedField(p => p.PackUnitOfMeasure, SpreadsheetDataType.String, ConvertToUnitOfMeasure, "Unit of Measure Code", "UnitofMeasureCode"),
                CreateNamedField(p => p.InventoryPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Inventory Posting Group", "InventoryPostingGroup"),
                CreateNamedField(p => p.UnitPrice, SpreadsheetDataType.Numeric, ConvertToDecimal,"Unit Price", "UnitPrice"),
                CreateNamedField(p => p.CostingMethod, SpreadsheetDataType.String, ConvertToCostingMethod, "Costing Method", "CostingMethod"),
                CreateNamedField(p => p.UnitCost, SpreadsheetDataType.Numeric, ConvertToDecimal, "Unit Cost", "UnitCost"),
                CreateNamedField(p => p.GeneralProductPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "Gen. Prod. Posting Group", "GenProdPostingGroup"),
                CreateNamedField(p => p.Country, SpreadsheetDataType.String, ConvertToCountry, "Country", "Country/Region of Origin Code"),
                CreateNamedField(p => p.GSTProductPostingGroup, SpreadsheetDataType.String, ConvertToPostingGroup, "GST Prod. Posting Group", "GSTProdPostingGroup"),
                CreateNamedField(p => p.ReplenishmentSystem, SpreadsheetDataType.String, ConvertToReplenishmentSystem, "Replenishment System", "ReplenishmentSystem"),
                CreateNamedField(p => p.ReorderingPolicy, SpreadsheetDataType.String, ConvertToReordingPolicy, "Reordering Policy", "ReorderingPolicy"),
                CreateNamedField(p => p.IncludeInventory, SpreadsheetDataType.Boolean, "Include Inventory", "IncludeInventory"),
                CreateNamedField(p => p.ReschedulingPeriod, SpreadsheetDataType.String, "Rescheduling Period", "ReschedulingPeriod"),
                CreateNamedField(p => p.LotAccumulationPeriod, SpreadsheetDataType.String, "Lot Accumulation Period", "LotAccumulationPeriod"),
                CreateNamedField(p => p.StyleCode, SpreadsheetDataType.String, "Style No.", "StyleNo.", "StyleNo"),
                CreateNamedField(p => p.StyleName, SpreadsheetDataType.String, ConvertToProperCase, "Description"),
                CreateNamedField(p => p.StyleContent, SpreadsheetDataType.String, ConvertToProperCase, "Content"),
                CreateNamedField(p => p.StyleLongDescription, SpreadsheetDataType.String, ConvertToProperCase, "Long Description", "LongDescription"),
                CreateNamedField(p => p.SizeName, SpreadsheetDataType.String, ConvertSizeName, "Size"),
                CreateNamedField(p => p.ColourName, SpreadsheetDataType.String, ConvertToProperCase, "Colour"),
                CreateNamedField(p => p.StyleGenderName, SpreadsheetDataType.String, ConvertToProperCase, "Gender"),
                CreateNamedField(p => p.StyleCategoryName, SpreadsheetDataType.String, ConvertToProperCase, "Category Style", "CategoryStyle"),
                CreateNamedField(p => p.ProductCategoryName, SpreadsheetDataType.String, ConvertToProperCase, "Product Category", "ProductCategory"),
                CreateNamedField(p => p.ProductTypeName, SpreadsheetDataType.String, ConvertToProperCase, "Product Type", "ProductType"),
                CreateNamedField(p => p.StyleClassName, SpreadsheetDataType.String, ConvertToProperCase, "Class/Style"),
                CreateNamedField(p => p.FabricName, SpreadsheetDataType.String, ConvertToProperCase, "Fabric"),
                CreateNamedField(p => p.StyleDepartmentName, SpreadsheetDataType.String, ConvertToProperCase, "Department"),
                CreateNamedField(p => p.FitName, SpreadsheetDataType.String, ConvertToProperCase, "Fit"),
                CreateNamedField(p => p.SizeRangeName, SpreadsheetDataType.String, ConvertToProperCase, "Size Range", "SizeRange"),
                CreateNamedField(p => p.BusinessDivisionName, SpreadsheetDataType.String, ConvertToProperCase, "Business Division", "BusinessDivision"),
                CreateNamedField(p => p.SeasonName, SpreadsheetDataType.String, ConvertToProperCase, "Season"),
                CreateNamedField(p => p.PriceIncludesGst, SpreadsheetDataType.Boolean, "Price Includes VAT", "PriceIncludesVAT"),
                CreateNamedField(p => p.AllowLineDiscount, SpreadsheetDataType.Boolean, "Allow Line Disc.", "AllowLineDisc.", "AllowLineDisc"),
                CreateNamedField(p => p.BarcodeType, SpreadsheetDataType.String, "Cross-Reference Type", "Cross-ReferenceType", "CrossReferenceType"),
                CreateNamedField(p => p.BarcodeNumber, SpreadsheetDataType.String, "Cross-Reference Type No.", "Cross-ReferenceTypeNo.", "CrossReferenceTypeNo"),
                CreateNamedField(p => p.WebStyleNo, SpreadsheetDataType.String, "Web Style No", "WebStyleNo"),
                CreateNamedField(p => p.WebStyleName, SpreadsheetDataType.String, ConvertToProperCase, "Web Style Name", "WebStyleName"),
                CreateNamedField(p => p.WebStyleDescription, SpreadsheetDataType.String, "Web Style Description", "WebStyleDescription"),
                CreateNamedField(p => p.WebStyleExtendedDescription, SpreadsheetDataType.String, ConvertToProperCase, "Web Style Extended Description", "WebStyleExtendedDescription"),
                CreateNamedField(p => p.WebStyleDetails, SpreadsheetDataType.String, "Web Details", "WebDetails"),
                CreateNamedField(p => p.WebStyleCategory, SpreadsheetDataType.String, ConvertToProperCase, "Web Category Style", "WebCategoryStyle"),
                CreateNamedField(p => p.WebStyleProductCategory, SpreadsheetDataType.String, ConvertToProperCase, "Web Product Category", "WebProductCategory"),
                CreateNamedField(p => p.WebStyleProductType, SpreadsheetDataType.String, ConvertToProperCase, "Web Product Type", "WebProductType"),
                CreateNamedField(p => p.WebStyleClass, SpreadsheetDataType.String, ConvertToProperCase, "Web Class/Style", "WebClass/Style", "WebClassStyle"),
                CreateNamedField(p => p.WebStyleIndustry, SpreadsheetDataType.String, ConvertToProperCase, "Web Industry", "WebIndustry"),
                CreateNamedField(p => p.WebStyleFabric, SpreadsheetDataType.String, ConvertToProperCase, "Web Fabric", "WebFabric"),
                CreateNamedField(p => p.WebStyleSizeRange, SpreadsheetDataType.String, ConvertToProperCase, "Web Size Range", "WebSizeRange"),
                CreateNamedField(p => p.WebStyleSizingTable, SpreadsheetDataType.String, ConvertToProperCase, "Web Sizing Table", "WebSizingTable"),
                CreateNamedField(p => p.WebStyleFit, SpreadsheetDataType.String, ConvertToProperCase, "Web Fitting Type", "WebFittingType"),
                CreateNamedField(p => p.WebStylePicture1, SpreadsheetDataType.String, "Style Pic", "StylePic", "Style Pic 1", "StylePic1"),
                CreateNamedField(p => p.WebStylePicture2, SpreadsheetDataType.String, "Style Pic 2", "StylePic2"),
                CreateNamedField(p => p.WebStylePicture3, SpreadsheetDataType.String, "Style Pic 3", "StylePic3"),
                CreateNamedField(p => p.WebSkuId, SpreadsheetDataType.String, "Web Sku", "WebSku", "Web Sku Id", "WebSkuId"),
                CreateNamedField(p => p.WebSkuSeason, SpreadsheetDataType.String, ConvertToProperCase, "Web Range", "WebRange"),
                CreateNamedField(p => p.WebSkuColour, SpreadsheetDataType.String, ConvertToProperCase, "Web Colour", "WebColour"),
                CreateNamedField(p => p.WebSkuFront, SpreadsheetDataType.String, "Web Front", "WebFront"),
                CreateNamedField(p => p.WebSkuBack, SpreadsheetDataType.String, "Web Back", "WebBack"),
                CreateNamedField(p => p.WebSkuSide, SpreadsheetDataType.String, "Web Side", "WebSide"), 
                CreateNamedField(p => p.WebSkuFull, SpreadsheetDataType.String, "Web Full", "WebFull"),
                CreateFieldGroup(new GroupableSpreadsheetFieldDefinitionBase[]
                {
                    CreateNamedField(p => p.PriceRangeMinimumQuantity, SpreadsheetDataType.Numeric, ConvertToInt32, "Cost Minimum Quantity {0}", "CostMinimumQuantity{0}"),
                    CreateNamedField(p => p.PriceRangeDirectUnitCost, SpreadsheetDataType.Numeric, ConvertToDecimal, "Direct Unit Cost {0}", "DirectUnitCost{0}"),
                    CreateNamedField(p => p.PriceRangeUnitPrice, SpreadsheetDataType.Numeric, ConvertToDecimal, "Unit Price {0}", "UnitPrice{0}"),
                })
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Converts a Size Name string.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The converted Size Name.</returns>
        private object ConvertSizeName(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // If the value is an integer, pad it to 2 digits
            int intValue;
            if (int.TryParse(stringValue, out intValue))
                return string.Format("{0:00}", intValue);

            // Return the value converted to proper case
            return ConvertToProperCase(value, currentRow);
        }

        /// <summary>
        /// Converts a string to proper case.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value converted to proper case.</returns>
        private object ConvertToProperCase(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Return the value converted to proper case
            return stringValue.Capitalize(ProperCaseExclusions);
        }

        /// <summary>
        /// Converts a string spreadsheet value to a parent Sku.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Sku.</returns>
        private object ConvertToParentSku(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to find the parent sku in the database
            var sku = _unitOfWork.Query<Sku>().FirstOrDefault(p => p.LegacyReference == stringValue);
            if (sku != null)
                return sku;

            // Error if no PostingGroup was found
            throw new SpreadsheetFieldException("Value must be the LegacyReference of an existing Sku.");
        }


        /// <summary>
        /// Converts a numeric spreadsheet value to a CostingMethod enum.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a CostingMethod enum.</returns>
        private object ConvertToCostingMethod(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to parse the string as a CostingMethod
            var costingMethod = EnumHelper.Parse<CostingMethod>(stringValue);
            if (costingMethod.HasValue)
                return costingMethod.Value;

            // Error if no CostingMethod was found
            throw new SpreadsheetFieldException("Value must be the Name of one of the pre-defined Costing Methods.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a ReplenishmentSystem enum.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a ReplenishmentSystem enum.</returns>
        private object ConvertToReplenishmentSystem(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to parse the string as a ReplenishmentSystem
            var replenishmentSystem = EnumHelper.Parse<ReplenishmentSystem>(stringValue);
            if (replenishmentSystem.HasValue)
                return replenishmentSystem.Value;

            // Error if no ReplenishmentSystem was found
            throw new SpreadsheetFieldException("Value must be the Name of one of the pre-defined Replenishment Systems.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a ReordingPolicy enum.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a ReordingPolicy enum.</returns>
        private object ConvertToReordingPolicy(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to parse the string as a ReorderingPolicy
            var reorderingPolicy = EnumHelper.Parse<ReorderingPolicy>(stringValue);
            if (reorderingPolicy.HasValue)
                return reorderingPolicy.Value;

            // Error if no ReorderingPolicy was found
            throw new SpreadsheetFieldException("Value must be the Name of one of the pre-defined Reordering Policies.");
        }

        /// <summary>
        /// Converts a numeric spreadsheet value to an Int32.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an Int32.</returns>
        private object ConvertToInt32(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is null
            if (value == null)
                return null;

            // Return the value as an Int32
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Converts a numeric spreadsheet value to a Decimal.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Decimal.</returns>
        private object ConvertToDecimal(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is null
            if (value == null)
                return null;

            // Return the value as a Decimal
            return Convert.ToDecimal(value);
        }

        /// <summary>
        /// Converts a string spreadsheet value to a PostingGroup object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an PostingGroup object.</returns>
        private object ConvertToPostingGroup(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Attempt to find the PostingGroup in the database
            var postingGroup = _unitOfWork.Query<PostingGroup>().FirstOrDefault(p => p.Code == stringValue);
            if (postingGroup != null)
                return postingGroup;

            // Error if no PostingGroup was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Posting Group record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a UnitOfMeasure object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a UnitOfMeasure object.</returns>
        private object ConvertToUnitOfMeasure(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the UnitOfMeasure in the database
            var unitOfMeasure = _unitOfWork.Query<UnitOfMeasure>().FirstOrDefault(p => p.Code == stringValue);
            if (unitOfMeasure != null)
                return unitOfMeasure;

            // Error if no GSTProductPostingGroup were found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Unit Of Measure record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Country object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Country object.</returns>
        private object ConvertToCountry(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Country in the database
            var country = _unitOfWork.Query<Country>().FirstOrDefault(p => p.Code == stringValue);
            if (country != null)
                return country;

            // Error if no GSTProductPostingGroup were found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Country record.");
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the InventoryFacade
                ConnectToFacade(_inventoryFacade);
                if (_inventoryFacade != null)
                    _unitOfWork = _inventoryFacade.CreateUnitOfWork();
            });
        }

        protected override void OnWidgetClosed(EventArgs e)
        {
            base.OnWidgetClosed(e);

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