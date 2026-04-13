using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Inventory.Uploader;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class StockAdjustmentImporterViewModel : ImporterViewModelBase<StockAdjustmentUploaderDataModel>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;
        private UnitOfWork _unitOfWork;

        #endregion


        #region Constructors

        public StockAdjustmentImporterViewModel(IInventoryFacade inventoryFacade)
            : this()
        {
            _inventoryFacade = inventoryFacade;
        }

        public StockAdjustmentImporterViewModel()
        {
            FirstHeaderCellReference = "A7";

            Fields.AddRange(new SpreadsheetFieldBase[]
            {
                CreateFixedField(p => p.DateReceived, SpreadsheetDataType.DateTime, "C2"),
                CreateFixedField(p => p.AdjustmentReason, SpreadsheetDataType.String, ConvertToStockAdjustmentReason, "C3"),
                CreateFixedField(p => p.Vendor, SpreadsheetDataType.String, ConvertToVendor, "F2"),
                CreateFixedField(p => p.ConNote, SpreadsheetDataType.String, "F3"),

                CreateNamedField(p => p.LegacyReference, SpreadsheetDataType.String, "Nav Id", "NavId", "Legacy Reference", "LegacyReference"),
                CreateNamedField(p => p.Style, SpreadsheetDataType.String, ConvertToStyle, "Style Code", "StyleCode"),
                CreateNamedField(p => p.Colour, SpreadsheetDataType.String, ConvertToColour, "Colour"),
                CreateNamedField(p => p.Size, SpreadsheetDataType.String, ConvertToSize, "Size"),
                CreateNamedField(p => p.Sku, SpreadsheetDataType.String, ConvertToSku, "Product Description", "ProductDescription", "Description"),
                CreateNamedField(p => p.Quantity, SpreadsheetDataType.Numeric, ConvertToQuantity, "Quantity"),
                CreateNamedField(p => p.TargetWarehouse, SpreadsheetDataType.String, ConvertToWarehouseLocation, "Target Warehouse", "TargetWarehouse"),
                CreateNamedField(p => p.TargetBin, SpreadsheetDataType.String, ConvertToTargetBinLocation, "Target Bin", "TargetBin"),
                CreateNamedField(p => p.TargetStockType, SpreadsheetDataType.String, ConvertToPhysicalStockType, "Target Stock Type", "TargetStockType"),
                CreateNamedField(p => p.SourceWarehouse, SpreadsheetDataType.String, ConvertToWarehouseLocation, "Source Warehouse", "SourceWarehouse"),
                CreateNamedField(p => p.SourceBin, SpreadsheetDataType.String, ConvertToSourceBinLocation, "Source Bin", "SourceBin"),
                CreateNamedField(p => p.SourceStockType, SpreadsheetDataType.String, ConvertToPhysicalStockType, "Source Stock Type", "SourceStockType"),
                CreateNamedField(p => p.Notes, SpreadsheetDataType.String, "Notes")
            });
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Converts a string spreadsheet value to a StockAdjustmentReason object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an StockAdjustmentReason object.</returns>
        private object ConvertToStockAdjustmentReason(object value, Dictionary<string, object> currentRow)
        {
            // Get the value as a string
            var stringValue = value as string;

            // Attempt to find the StockAdjustmentReason in the database
            var stockAdjustmentReason = _unitOfWork.Query<StockAdjustmentReason>().FirstOrDefault(p => p.Name == stringValue);
            if (stockAdjustmentReason != null)
                return stockAdjustmentReason;

            // Error if no StockAdjustmentReason was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Stock Adjustment Reason record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Contact object, which must be flagged as a Vendor.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Contact object.</returns>
        private object ConvertToVendor(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the vendor Contact in the database
            var contact = _unitOfWork.Query<Contact>().FirstOrDefault(p => p.LegacyReference == stringValue && p.IsVendor);
            if (contact != null)
                return contact;

            // Error if no vendor Contact was found
            throw new SpreadsheetFieldException("Value must be the Reference of an existing Contact record where IsVendor is true.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Style object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Style object.</returns>
        private object ConvertToStyle(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Style in the database
            var style = _unitOfWork.Query<Style>().FirstOrDefault(p => p.Code == stringValue);
            if (style != null)
                return style;

            // Error if no Style was found
            throw new SpreadsheetFieldException("Value must be the Code of an existing Style record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Colour object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Colour object.</returns>
        private object ConvertToColour(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Colour in the database
            var colour = _unitOfWork.Query<Colour>().FirstOrDefault(p => p.Name == stringValue);
            if (colour != null)
                return colour;

            // Error if no Colour was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Colour record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a Size object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a Size object.</returns>
        private object ConvertToSize(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the Size in the database
            var size = _unitOfWork.Query<Size>().FirstOrDefault(p => p.Name == stringValue);
            if (size != null)
                return size;

            // Error if no Size was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Size record.");
        }

        /// <summary>
        /// Attempts to find a Sku based on values collected in other columns.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>A Sku object.</returns>
        private object ConvertToSku(object value, Dictionary<string, object> currentRow)
        {
            Sku sku;

            // If a LegacyReference was supplied, attempt to find a Sku using that
            var legacyReference = currentRow["LegacyReference"] as string;
            if (!string.IsNullOrWhiteSpace(legacyReference))
            {
                // Attempt to find the Sku in the database
                sku = _unitOfWork.Query<Sku>().FirstOrDefault(p => p.LegacyReference == legacyReference);
                if (sku != null)
                    return sku;

                // Error if no Sku was found
                throw new SpreadsheetFieldException("When LegacyReference is supplied, it must match the LegacyReference of an existing Sku record.");
            }

            // If a NavId was not supplied, attempt to find a Sku using Style/Colour/Size
            var style = currentRow["Style"] as Style;
            var colour = currentRow["Colour"] as Colour;
            var size = currentRow["Size"] as Size;

            // Error if Style, Color or Size are null
            if (style == null || colour == null || size == null)
                throw new SpreadsheetFieldException("When LegacyReference is not supplied, Style Code, Colour and Size must match to an existing Sku record.");

            // Attempt to find the Sku in the database
            sku = _unitOfWork.Query<Sku>().FirstOrDefault(p => p.Style.Oid == style.Oid && p.Colour.Oid == colour.Oid && p.Size.Oid == size.Oid);
            if (sku != null)
                return sku;

            // Error if no Sku was found
            throw new SpreadsheetFieldException("When LegacyReference is not supplied, Style Code, Colour and Size must match to an existing Sku record.");
        }

        /// <summary>
        /// Converts a numeric spreadsheet value to an Int32, which must be greater than zero.
        /// </summary>
        /// <param name="value">The value to convert (double).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as an Int32.</returns>
        private object ConvertToQuantity(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is null
            if (value == null)
                return null;

            // Convert the value to an Int32
            var quantity = Convert.ToInt32(value);

            // Error if the Quantity is less than one
            if (quantity < 1)
                throw new SpreadsheetFieldException("Quantity must be greater than zero.");

            // Return the quantity
            return quantity;
        }

        /// <summary>
        /// Converts a string spreadsheet value to a WarehouseLocation object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a WarehouseLocation object.</returns>
        private object ConvertToWarehouseLocation(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the WarehouseLocation in the database
            var warehouseLocation = _unitOfWork.Query<WarehouseLocation>().FirstOrDefault(p => p.Name == stringValue);
            if (warehouseLocation != null)
                return warehouseLocation;

            // Error if no WarehouseLocation was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Warehouse Location record.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a BinLocation object that exists in the Target Warehouse.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a BinLocation object.</returns>
        private object ConvertToTargetBinLocation(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to get the Target Warehouse
            var targetWarehouse = currentRow["TargetWarehouse"] as WarehouseLocation;
            if (targetWarehouse == null)
                throw new SpreadsheetFieldException("Unable to validate Target Bin as Target Warehouse is invalid.");

            // Attempt to find the BinLocation in the database
            var binLocation = _unitOfWork.Query<BinLocation>().FirstOrDefault(p => p.Name == stringValue && p.WarehouseLocation.Oid == targetWarehouse.Oid);
            if (binLocation != null)
                return binLocation;

            // Error if no BinLocation was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Bin Location record which exists within the Target Warehouse.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a BinLocation object that exists in the Source Warehouse.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a BinLocation object.</returns>
        private object ConvertToSourceBinLocation(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to get the Source Warehouse
            var sourceWarehouse = currentRow["SourceWarehouse"] as WarehouseLocation;
            if (sourceWarehouse == null)
                throw new SpreadsheetFieldException("Unable to validate Source Bin as Source Warehouse is invalid.");

            // Attempt to find the BinLocation in the database
            var binLocation = _unitOfWork.Query<BinLocation>().FirstOrDefault(p => p.Name == stringValue && p.WarehouseLocation.Oid == sourceWarehouse.Oid);
            if (binLocation != null)
                return binLocation;

            // Error if no BinLocation was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Bin Location record which exists within the Source Warehouse.");
        }

        /// <summary>
        /// Converts a string spreadsheet value to a PhysicalStockType object.
        /// </summary>
        /// <param name="value">The value to convert (string).</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value as a PhysicalStockType object.</returns>
        private object ConvertToPhysicalStockType(object value, Dictionary<string, object> currentRow)
        {
            // Abort if the value is empty
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Attempt to find the PhysicalStockType in the database
            var warehouseLocation = _unitOfWork.Query<PhysicalStockType>().FirstOrDefault(p => p.Name == stringValue);
            if (warehouseLocation != null)
                return warehouseLocation;

            // Error if no PhysicalStockType was found
            throw new SpreadsheetFieldException("Value must be the Name of an existing Physical Stock Type record.");
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