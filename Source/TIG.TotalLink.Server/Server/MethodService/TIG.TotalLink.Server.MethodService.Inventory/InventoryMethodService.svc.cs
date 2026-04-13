using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using DevExpress.Xpo;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Contract.Core.Exception;
using TIG.TotalLink.Shared.Contract.Inventory;
using TIG.TotalLink.Shared.DataModel.Admin.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Inventory;
using TIG.TotalLink.Shared.Xpo.Core.Helper;

namespace TIG.TotalLink.Server.MethodService.Inventory
{
    public class InventoryMethodService : MethodServiceBase, IInventoryMethodService
    {
        #region Private Fields

        private static IInventoryFacade _inventoryFacade;

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a new StockAdjustment, and updates related PhysicalStock entries.
        /// </summary>
        /// <param name="stockAdjustmentJson">Details for the new StockAdjustment, serialized as a Json string.</param>
        /// <returns>An array of changes that occurred.</returns>
        public EntityChange[] AddStockAdjustment(string stockAdjustmentJson)
        {
            // Get facades
            var inventoryFacade = GetInventoryDataFacade();
            var changes = new List<EntityChange>();

            try
            {
                // Deserialize the StockAdjustment
                var deserializedStockAdjustment = JsonHelper.DeserializeDataObject<StockAdjustment>(stockAdjustmentJson);

                using (var uow = inventoryFacade.CreateUnitOfWork())
                {
                    // Impersonate the client user
                    uow.ImpersonateUser(ServiceHelper.GetCurrentAuthenticationToken());

                    // Create a new StockAdjustment
                    var newStockAdjustment = new StockAdjustment(uow)
                    {
                        Sku = uow.GetDataObject(deserializedStockAdjustment.Sku),
                        Quantity = deserializedStockAdjustment.Quantity,
                        Notes = deserializedStockAdjustment.Notes,
                        Reason = uow.GetDataObject(deserializedStockAdjustment.Reason),
                        TargetBinLocation = uow.GetDataObject(deserializedStockAdjustment.TargetBinLocation),
                        TargetPhysicalStockType = uow.GetDataObject(deserializedStockAdjustment.TargetPhysicalStockType),
                        SourceBinLocation = uow.GetDataObject(deserializedStockAdjustment.SourceBinLocation),
                        SourcePhysicalStockType = uow.GetDataObject(deserializedStockAdjustment.SourcePhysicalStockType),
                        Vendor = uow.GetDataObject(deserializedStockAdjustment.Vendor),
                        VendorReference = deserializedStockAdjustment.VendorReference
                    };

                    // Adjust target PhysicalStock entries
                    var targetPhysicalStock = AddOrUpdatePhysicalStock(uow, newStockAdjustment.Sku, (newStockAdjustment.Reason.IsTargetIncrease ? newStockAdjustment.Quantity : -newStockAdjustment.Quantity), newStockAdjustment.TargetBinLocation, newStockAdjustment.TargetPhysicalStockType);
                    var targetPhysicalStockIsNew = (targetPhysicalStock.Oid == Guid.Empty);

                    // Adjust source PhysicalStock entries
                    PhysicalStock sourcePhysicalStock = null;
                    var sourcePhysicalStockIsNew = false;
                    if (newStockAdjustment.Reason.IsSourceIncrease.HasValue)
                    { 
                        sourcePhysicalStock = AddOrUpdatePhysicalStock(uow, newStockAdjustment.Sku, (newStockAdjustment.Reason.IsSourceIncrease.Value ? newStockAdjustment.Quantity : -newStockAdjustment.Quantity), newStockAdjustment.SourceBinLocation, newStockAdjustment.SourcePhysicalStockType);
                        sourcePhysicalStockIsNew = (sourcePhysicalStock.Oid == Guid.Empty);
                    }

                    // Commit changes
                    uow.CommitChanges();

                    // Record the changed entities
                    changes.Add(new EntityChange(newStockAdjustment, EntityChange.ChangeTypes.Add));
                    changes.Add(new EntityChange(targetPhysicalStock, (targetPhysicalStockIsNew ? EntityChange.ChangeTypes.Add : EntityChange.ChangeTypes.Modify)));

                    if (sourcePhysicalStock != null)
                        changes.Add(new EntityChange(sourcePhysicalStock, (sourcePhysicalStockIsNew ? EntityChange.ChangeTypes.Add : EntityChange.ChangeTypes.Modify)));
                }
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to create Stock Adjustment!", ex));
            }

            // Return the list of changes
            return changes.ToArray();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns an InventoryFacade connected to the data service only.
        /// </summary>
        /// <returns>A InventoryFacade.</returns>
        private static IInventoryFacade GetInventoryDataFacade()
        {
            if (_inventoryFacade == null)
                _inventoryFacade = new InventoryFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_inventoryFacade != null && !_inventoryFacade.IsDataConnected)
                    _inventoryFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Inventory Facade!", ex));
            }

            return _inventoryFacade;
        }

        /// <summary>
        /// Adds or updates a PhyscialStock record for the specified Sku.
        /// </summary>
        /// <param name="uow">The UnitOfWork to use to modify the PhysicalStock.</param>
        /// <param name="sku">The Sku to modify.</param>
        /// <param name="quantity">The Quantity to apply.</param>
        /// <param name="binLocation">The BinLocation to modify.</param>
        /// <param name="physicalStockType">The PhysicalStockType to modify.</param>
        private PhysicalStock AddOrUpdatePhysicalStock(UnitOfWork uow, Sku sku, int quantity, BinLocation binLocation, PhysicalStockType physicalStockType)
        {
            // Attempt to find an existing PhysicalStock record for the specified Sku, BinLocation and PhysicalStockType
            var physicalStock = uow.Query<PhysicalStock>().FirstOrDefault(p => p.Sku.Oid == sku.Oid && p.BinLocation.Oid == binLocation.Oid && p.PhysicalStockType.Oid == physicalStockType.Oid);

            // If a PhysicalStock was found, update the quantity on it
            if (physicalStock != null)
            {
                // Throw an error if the quantity will be reduced below zero
                if (physicalStock.Quantity + quantity < 0)
                    throw new ServiceMethodException("This adjustment would reduce the Physical Stock below zero!");

                physicalStock.Quantity += quantity;
                return physicalStock;
            }

            // Throw an error if the quantity is below zero
            if (quantity < 0)
                throw new ServiceMethodException("This adjustment would reduce the Physical Stock below zero!");

            // If no PhysicalStock was found, create a new entry
            physicalStock = new PhysicalStock(uow)
            {
                Sku = sku,
                BinLocation = binLocation,
                PhysicalStockType = physicalStockType,
                Quantity = quantity
            };
            return physicalStock;
        }

        #endregion
    }
}
