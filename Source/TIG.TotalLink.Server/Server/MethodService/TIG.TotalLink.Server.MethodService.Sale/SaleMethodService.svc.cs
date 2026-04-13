using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Contract.Core.Exception;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.DataModel.Admin.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Sale;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Facade.Sale;
using TIG.TotalLink.Shared.Xpo.Core.Helper;

namespace TIG.TotalLink.Server.MethodService.Sale
{
    public class SaleMethodService : MethodServiceBase, ISaleMethodService
    {
        #region Private Constants

        private const int MaxReleaseAttempts = 10;

        #endregion


        #region Private Fields

        private static IGlobalFacade _globalFacade;
        private static IAdminFacade _adminFacade;
        private static ISaleFacade _saleFacade;
        private static readonly int _systemCode;
        private static readonly string _referenceValueFormat;

        #endregion


        #region Constructors

        static SaleMethodService()
        {
            // Get facades
            var globalFacade = GetGlobalDataFacade();

            // Collect global settings
            _systemCode = Convert.ToInt32(globalFacade.GetSetting("SystemCode"));
            _referenceValueFormat = globalFacade.GetSetting("ReferenceValueFormat");
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Releases items on a Sales Order.
        /// </summary>
        /// <param name="parameters">A ReleaseSalesOrderParameters object which describes the items to release.</param>
        /// <returns>A ReleaseSalesOrderResult object containing results of the operation.</returns>
        public ReleaseSalesOrderResult ReleaseSalesOrder(ReleaseSalesOrderParameters parameters)
        {
            // Get facades and prepare data
            var saleFacade = GetSaleDataFacade();
            var adminFacade = GetAdminMethodFacade();
            var result = new ReleaseSalesOrderResult();
            var changes = new List<EntityChange>();
            string releaseErrorMessage = null;
            Delivery delivery = null;
            var allowPartialDelivery = parameters.AllowPartialDelivery;

            try
            {
                // To make sure we get up-to-date data, flag the SalesOrder tables as dirty
                saleFacade.NotifyDirtyTypes(typeof(SalesOrder), typeof(SalesOrderItem));

                using (var uow = saleFacade.CreateUnitOfWork())
                {
                    var innerChanges = new List<EntityChange>();

                    // Impersonate the client user
                    uow.ImpersonateUser(ServiceHelper.GetCurrentAuthenticationToken());

                    // Get the SalesOrder
                    var salesOrder = uow.GetObjectByKey<SalesOrder>(parameters.SalesOrderOid);
                    if (salesOrder == null)
                        throw new FaultException<ServiceFault>(new ServiceFault("Failed to find SalesOrder to release."));

                    // If AllowPartialDelivery has not been overridden in the parameters, use the value from the SalesOrder
                    if (!allowPartialDelivery.HasValue)
                        allowPartialDelivery = salesOrder.AllowPartialDelivery;

                    SalesOrderRelease salesOrderRelease;
                    if (parameters.SalesOrderReleaseOid.HasValue)
                    {
                        // If a SalesOrderReleaseOid was supplied, get the existing SalesOrderRelease to append to
                        salesOrderRelease = uow.GetObjectByKey<SalesOrderRelease>(parameters.SalesOrderReleaseOid);
                        if (salesOrderRelease == null)
                            throw new FaultException<ServiceFault>(new ServiceFault("Failed to find existing SalesOrderRelease."));

                        // Add the SalesOrder to the SalesOrderRelease
                        salesOrderRelease.SalesOrders.Add(salesOrder);
                    }
                    else
                    {
                        // If no SalesOrderReleaseOid was supplied, create a new SalesOrderRelease
                        salesOrderRelease = new SalesOrderRelease(uow)
                        {
                            Oid = Guid.NewGuid()
                        };
                        salesOrderRelease.SalesOrders.Add(salesOrder);
                        innerChanges.Add(new EntityChange(salesOrderRelease, EntityChange.ChangeTypes.Add));
                        result.SalesOrderReleaseOid = salesOrderRelease.Oid;

                        // Generate and store a reference number for the SalesOrderRelease
                        var salesOrderSequenceNumberResult = adminFacade.GetNextSequenceNumber(salesOrderRelease.GetType().Name);
                        salesOrderRelease.Reference = ReferenceNumberHelper.FormatValue(_systemCode, salesOrderSequenceNumberResult.SequenceCode, salesOrderSequenceNumberResult.SequenceNumber, _referenceValueFormat);
                        innerChanges.AddRange(salesOrderSequenceNumberResult.EntityChanges);

                        // Create BinLocation links
                        foreach (var binLocationOid in parameters.BinLocationOids)
                        {
                            var binLocationLink = new SalesOrderRelease_BinLocation(uow)
                            {
                                SalesOrderRelease = salesOrderRelease,
                                BinLocation = uow.GetObjectByKey<BinLocation>(binLocationOid)
                            };
                            innerChanges.Add(new EntityChange(binLocationLink, EntityChange.ChangeTypes.Add));
                        }

                        // Create PhysicalStockType links
                        foreach (var physicalStockTypeOid in parameters.PhysicalStockTypeOids)
                        {
                            var physicalStockTypeLink = new SalesOrderRelease_PhysicalStockType(uow)
                            {
                                SalesOrderRelease = salesOrderRelease,
                                PhysicalStockType = uow.GetObjectByKey<PhysicalStockType>(physicalStockTypeOid)
                            };
                            innerChanges.Add(new EntityChange(physicalStockTypeLink, EntityChange.ChangeTypes.Add));
                        }
                    }

                    // If partial delivery is not allowed and SalesOrderItems have been supplied in the parameters...
                    if (!allowPartialDelivery.Value && parameters.SalesOrderItems != null && parameters.SalesOrderItems.Count > 0)
                    {
                        // Fail the release if the quantity being released does not exactly match the quantity remaining on the sales order
                        var quantityRemaining = salesOrder.TotalQuantity - salesOrder.TotalQuantityCancelled - salesOrder.TotalQuantityReleased;
                        var quantityToRelease = parameters.SalesOrderItems.Sum(s => s.QuantityToRelease);
                        if (quantityRemaining != quantityToRelease)
                        {
                            releaseErrorMessage = "Attempted to partially release a Sales Order but Allow Partial Delivery is disabled.";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(releaseErrorMessage))
                    {
                        // Create a new Delivery
                        // TODO : DeliveryStatus should be found by Order instead of Name
                        delivery = new Delivery(uow)
                        {
                            Oid = Guid.NewGuid(),
                            SalesOrderRelease = salesOrderRelease,
                            Contact = salesOrder.Contact,
                            Status = uow.Query<DeliveryStatus>().FirstOrDefault(s => s.Name == "Generating")
                        };
                        innerChanges.Add(new EntityChange(delivery, EntityChange.ChangeTypes.Add));

                        // Generate and store a reference number for the Delivery
                        var deliverySequenceNumberResult = adminFacade.GetNextSequenceNumber(delivery.GetType().Name);
                        delivery.Reference = ReferenceNumberHelper.FormatValue(_systemCode,
                            deliverySequenceNumberResult.SequenceCode, deliverySequenceNumberResult.SequenceNumber,
                            _referenceValueFormat);
                        innerChanges.AddRange(deliverySequenceNumberResult.EntityChanges);
                    }

                    // Commit the parent items
                    uow.CommitChanges();

                    // Record the changed entities
                    changes.AddRange(innerChanges);

                    // If no SalesOrderItems were specified, create SalesOrderItemParameters to release all the remaining items on the SalesOrder
                    if (parameters.SalesOrderItems == null || parameters.SalesOrderItems.Count == 0)
                    {
                        foreach (var salesOrderItem in salesOrder.SalesOrderItems.Where(i => i.Quantity > i.QuantityCancelled + i.QuantityReleased))
                        {
                            parameters.AddSalesOrderItem(salesOrderItem.Oid, salesOrderItem.Quantity - salesOrderItem.QuantityCancelled - salesOrderItem.QuantityReleased);
                        }
                    }

                    // Attempt to release the specified quantities
                    if (parameters.SalesOrderItems != null)
                    {
                        foreach (var salesOrderItemParameter in parameters.SalesOrderItems)
                        {
                            if (string.IsNullOrWhiteSpace(releaseErrorMessage))
                            {
                                // If no global release error has been recorded yet, attempt to release the item
                                releaseErrorMessage = ReleaseSalesOrderItem(parameters, salesOrderItemParameter, delivery, allowPartialDelivery.Value, uow, changes);
                            }
                            else
                            {
                                // If a global release error has been recorded, just write the error without attempting to release
                                CreateFailedReleaseItem(releaseErrorMessage, salesOrderItemParameter, salesOrderRelease, uow, changes);
                            }
                        }
                    }

                    if (delivery != null)
                    {
                        if (!string.IsNullOrWhiteSpace(releaseErrorMessage) || delivery.DeliveryItems.Count == 0)
                        {
                            // If a global release error occurred or nothing got released, delete the Delivery
                            delivery.Delete();
                            uow.CommitChanges();
                            changes.RemoveAll(c => c.EntityType == typeof(Delivery));
                        }
                        else
                        {
                            // If something got released, update the status on the Delivery
                            // TODO : DeliveryStatus should be found by Order instead of Name
                            delivery.Status = uow.Query<DeliveryStatus>().FirstOrDefault(s => s.Name == "Awaiting Picking and Dispatch");

                            // Add the DeliveryOid and the TotalQuantityReleased to the results
                            result.DeliveryOid = delivery.Oid;
                            result.TotalQuantityReleased = delivery.DeliveryItems.Sum(d => d.Quantity);

                            // Update the SalesOrder Status
                            // TODO : How do we find correct SalesOrderStatus for all/part released?
                            salesOrder.Status = salesOrder.SalesOrderItems.Sum(s => s.QuantityReleased + s.QuantityCancelled) >= salesOrder.SalesOrderItems.Sum(s => s.Quantity)
                                ? uow.Query<SalesOrderStatus>().FirstOrDefault(s => s.Name == "Released")
                                : uow.Query<SalesOrderStatus>().FirstOrDefault(s => s.Name == "Part Released");
                            uow.CommitChanges();
                            changes.Add(new EntityChange(salesOrder, EntityChange.ChangeTypes.Modify));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }

            // Return the result
            result.Changes = changes.ToArray();
            return result;
        }

        /// <summary>
        /// Releases items on a Delivery.
        /// </summary>
        /// <param name="parameters">A ReleaseDeliveryParameters object which describes the items to release.</param>
        /// <returns>A ReleaseDeliveryResult object containing results of the operation.</returns>
        public ReleaseDeliveryResult ReleaseDelivery(ReleaseDeliveryParameters parameters)
        {
            // Get facades and prepare data
            var saleFacade = GetSaleDataFacade();
            var result = new ReleaseDeliveryResult();
            var changes = new List<EntityChange>();
            var totalQuantityPicked = 0;
            var deliveryDispatched = false;

            try
            {
                // To make sure we get up-to-date data, flag the Delivery tables as dirty
                saleFacade.NotifyDirtyTypes(typeof(Delivery), typeof(DeliveryItem), typeof(PickItem));

                using (var uow = saleFacade.CreateUnitOfWork())
                {
                    var innerChanges = new List<EntityChange>();

                    // Impersonate the client user
                    uow.ImpersonateUser(ServiceHelper.GetCurrentAuthenticationToken());

                    // Get the Delivery
                    var delivery = uow.GetObjectByKey<Delivery>(parameters.DeliveryOid);
                    if (delivery == null)
                        throw new FaultException<ServiceFault>(new ServiceFault("Failed to find Delivery to release."));

                    foreach (var pickItemParameter in parameters.PickItems)
                    {
                        // Get the PickItem
                        var pickItem = uow.GetObjectByKey<PickItem>(pickItemParameter.PickItemOid);
                        if (pickItem == null)
                            throw new FaultException<ServiceFault>(new ServiceFault("Failed to find Pick Item to release."));

                        // Throw an error if the PickItem is not ready to be picked
                        if (!pickItem.CanBePicked)
                            throw new ServiceMethodException("This Delivery is not ready to be picked.");

                        // Throw an error if the PickItem does not belong to the Delivery supplied in the parameters
                        if (pickItem.DeliveryItem.Delivery.Oid != parameters.DeliveryOid)
                            throw new ServiceMethodException("One of the Pick Items does not belong to the specified Delivery.");

                        // Throw an error if they are attempting to pick more items than are remaining on the PickItem
                        if (pickItemParameter.QuantityToPick > pickItem.Quantity - pickItem.QuantityPicked)
                            throw new ServiceMethodException("Attempted to release more than the quantity remaining on one of the Pick Items.");

                        // Apply the QuantityToPick
                        pickItem.QuantityPicked += pickItemParameter.QuantityToPick;
                        pickItem.DeliveryItem.QuantityReleased += pickItemParameter.QuantityToPick;
                        innerChanges.Add(new EntityChange(pickItem, EntityChange.ChangeTypes.Modify));
                        totalQuantityPicked += pickItemParameter.QuantityToPick;
                    }

                    // If the Delivery is being dispatched...
                    if (!string.IsNullOrWhiteSpace(parameters.ConsignmentNote))
                    {
                        // Throw an error if the Delivery is not ready to be dispatched
                        if (!delivery.Status.CanBeDispatched)
                            throw new ServiceMethodException("This Delivery is not ready to be dispatched.");

                        // Throw an error if the Delivery has already been dispatched
                        if (!string.IsNullOrWhiteSpace(delivery.ConsignmentNote))
                            throw new ServiceMethodException("This Delivery has already been dispatched.");

                        // Apply the ConsignmentNote
                        // TODO : How do we find the correct next DeliveryStatus here?
                        delivery.ConsignmentNote = parameters.ConsignmentNote;
                        delivery.Status = uow.Query<DeliveryStatus>().FirstOrDefault(s => s.Name == "Dispatched");
                        innerChanges.Add(new EntityChange(delivery, EntityChange.ChangeTypes.Modify));
                        deliveryDispatched = true;

                        // Create StockAdjustments for all QuantityPicked on the PickItems
                        // (We do this directly here instead of calling the InventoryMethodService
                        // because we need the StockAdjustments and the new Delivery status to be written at the same time)
                        foreach (var pickItem in delivery.DeliveryItems.SelectMany(d => d.PickItems))
                        {
                            // Create a new StockAdjustment
                            // TODO : How do we find the correct StockAdjustmentReason here?
                            var stockAdjustment = new StockAdjustment(uow)
                            {
                                Sku = pickItem.DeliveryItem.Sku,
                                Quantity = pickItem.QuantityPicked,
                                Notes = string.Format("Dispatched on Delivery {0}", delivery.Reference),
                                Reason = uow.Query<StockAdjustmentReason>().FirstOrDefault(s => s.Name == "Dispatched"),
                                TargetBinLocation = pickItem.BinLocation,
                                TargetPhysicalStockType = pickItem.PhysicalStockType
                            };
                            innerChanges.Add(new EntityChange(stockAdjustment, EntityChange.ChangeTypes.Add));

                            // Attempt to find an existing PhysicalStock record for the specified Sku, BinLocation and PhysicalStockType
                            var physicalStock = uow.Query<PhysicalStock>().FirstOrDefault(p => p.Sku.Oid == stockAdjustment.Sku.Oid && p.BinLocation.Oid == stockAdjustment.TargetBinLocation.Oid && p.PhysicalStockType.Oid == stockAdjustment.TargetPhysicalStockType.Oid);
                            if (physicalStock == null)
                                throw new FaultException<ServiceFault>(new ServiceFault("Failed to find a Physical Stock entry for one of the Pick Items."));

                            // Throw an error if the quantity will be reduced below zero
                            if (physicalStock.Quantity - stockAdjustment.Quantity < 0)
                                throw new ServiceMethodException("One of the Pick Items would reduce the Physical Stock below zero.");

                            // Reduce the PhysicalStock
                            physicalStock.Quantity -= stockAdjustment.Quantity;
                            innerChanges.Add(new EntityChange(physicalStock, EntityChange.ChangeTypes.Modify));
                        }
                    }

                    // Commit all changes
                    uow.CommitChanges();

                    // Record the changed entities
                    changes.AddRange(innerChanges);
                    result.TotalQuantityPicked = totalQuantityPicked;
                    result.DeliveryDispatched = deliveryDispatched;
                }
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }

            // Return the result
            result.Changes = changes.ToArray();
            return result;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns an GlobalFacade connected to the data service only.
        /// </summary>
        /// <returns>A GlobalFacade.</returns>
        private static IGlobalFacade GetGlobalDataFacade()
        {
            if (_globalFacade == null)
                _globalFacade = new GlobalFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_globalFacade != null && !_globalFacade.IsDataConnected)
                    _globalFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Global Facade!", ex));
            }

            return _globalFacade;
        }

        /// <summary>
        /// Initializes and returns an AdminFacade connected to the method service only.
        /// </summary>
        /// <returns>An AdminFacade.</returns>
        private static IAdminFacade GetAdminMethodFacade()
        {
            if (_adminFacade == null)
                _adminFacade = new AdminFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_adminFacade != null && !_adminFacade.IsMethodConnected)
                    _adminFacade.Connect(ServiceTypes.Method);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Admin Facade!", ex));
            }

            return _adminFacade;
        }

        /// <summary>
        /// Initializes and returns a SaleFacade connected to the data service only.
        /// </summary>
        /// <returns>A SaleFacade.</returns>
        private static ISaleFacade GetSaleDataFacade()
        {
            if (_saleFacade == null)
                _saleFacade = new SaleFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_saleFacade != null && !_saleFacade.IsDataConnected)
                    _saleFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Sale Facade!", ex));
            }

            return _saleFacade;
        }

        /// <summary>
        /// Releases one item on a SalesOrder.
        /// </summary>
        /// <param name="releaseParameters">An object containing information about the SalesOrder to release.</param>
        /// <param name="salesOrderItemParameter">An object containing information about the SalesOrderItem to release.</param>
        /// <param name="delivery">The parent Delivery.</param>
        /// <param name="allowPartialDelivery">Indicates if the Sales Order can be part released.</param>
        /// <param name="uow">The UnitOfWork for updating the data.</param>
        /// <param name="changes">A list of entity changes that have occurred.</param>
        /// <returns>Null if remaining items should be processed; otherwise a string containing an error message to be applied to all remaining items.</returns>
        private string ReleaseSalesOrderItem(ReleaseSalesOrderParameters releaseParameters, SalesOrderItemParameter salesOrderItemParameter, Delivery delivery, bool allowPartialDelivery, UnitOfWork uow, List<EntityChange> changes)
        {
            string releaseErrorMessage = null;

            // Get the related SalesOrderItem
            var salesOrderItem = uow.GetObjectByKey<SalesOrderItem>(salesOrderItemParameter.SalesOrderItemOid);

            try
            {
                // Throw an error if the SalesOrderItem does not belong to the SalesOrder supplied in the releaseParameters
                if (salesOrderItem.SalesOrder.Oid != releaseParameters.SalesOrderOid)
                    throw new ServiceMethodException("This item does not belong to the specified Sales Order.");

                // Throw an error if they are attempting to release more items than are remaining on the SalesOrderItem
                if (salesOrderItemParameter.QuantityToRelease > salesOrderItem.Quantity - salesOrderItem.QuantityCancelled - salesOrderItem.QuantityReleased)
                    throw new ServiceMethodException("Attempted to release more than the quantity remaining on the Sales Order Item.");

                var attemptsRemaining = MaxReleaseAttempts;

                while (true)
                {
                    try
                    {
                        var innerChanges = new List<EntityChange>();

                        // To make sure we get up-to-date data, flag all tables that affect stock levels as dirty
                        _saleFacade.NotifyDirtyTypes(typeof(PhysicalStock), typeof(Sku), typeof(BinLocation), typeof(PhysicalStockType), typeof(Delivery), typeof(DeliveryItem), typeof(PickItem));

                        // Get all the PhysicalStocks that may supply stock for this item
                        // TODO : PhysicalStock should be ordered so the user can control which BinLocations and PhysicalStockTypes get allocated first
                        var physicalStocks = uow.Query<PhysicalStock>()
                            .Where(p => p.AvailableStock > 0 && p.Sku.Oid == salesOrderItem.Sku.Oid && releaseParameters.BinLocationOids.Contains(p.BinLocation.Oid) && releaseParameters.PhysicalStockTypeOids.Contains(p.PhysicalStockType.Oid))
                            .ToList();

                        // Create PickItems to allocate stock from each of the available PhysicalStocks
                        var quantityRemaining = salesOrderItemParameter.QuantityToRelease;
                        var physicalStockIndex = 0;
                        var pickItems = new List<PickItem>();
                        while (quantityRemaining > 0 && physicalStockIndex < physicalStocks.Count)
                        {
                            // Calculate the quantity that can be released from this PhysicalStock
                            var physicalStock = physicalStocks[physicalStockIndex];
                            var quantityToRelease = Math.Min(quantityRemaining, physicalStock.AvailableStock);
                            if (quantityToRelease > 0)
                            {
                                // Manually increment the OptimisticLockField on the PhysicalStock
                                // This will trigger a LockingException on commit if anyone else has modified the PhysicalStockType.Quantity or released some of the items
                                physicalStock.SetMemberValue("OptimisticLockField", (int)physicalStock.GetMemberValue("OptimisticLockField") + 1);
                                innerChanges.Add(new EntityChange(physicalStock, EntityChange.ChangeTypes.Modify));
                                innerChanges.Add(new EntityChange(physicalStock.Sku, EntityChange.ChangeTypes.Modify));

                                // Create a PickItem to allocate the stock
                                var pickItem = new PickItem(uow)
                                {
                                    BinLocation = physicalStock.BinLocation,
                                    PhysicalStockType = physicalStock.PhysicalStockType,
                                    Quantity = quantityToRelease
                                };
                                pickItems.Add(pickItem);
                                innerChanges.Add(new EntityChange(pickItem, EntityChange.ChangeTypes.Add));
                            }

                            // Reduce the quantityRemaining and move to the next PhysicalStock
                            quantityRemaining -= quantityToRelease;
                            physicalStockIndex++;
                        }

                        // If partial delivery is disabled and we could not release all items...
                        if (!allowPartialDelivery && quantityRemaining > 0)
                        {
                            // Set an error for all remaining items and throw it
                            releaseErrorMessage = "Sales Order could only be partially released but Allow Partial Delivery is disabled.";
                            throw new ServiceMethodException(releaseErrorMessage);
                        }

                        // Throw an error if nothing was released
                        var quantityReleased = salesOrderItemParameter.QuantityToRelease - quantityRemaining;
                        if (quantityReleased == 0)
                            throw new ServiceMethodException("Failed to find any available stock.");

                        // Update the SalesOrderItem
                        salesOrderItem.QuantityReleased += quantityReleased;
                        innerChanges.Add(new EntityChange(salesOrderItem, EntityChange.ChangeTypes.Modify));

                        // Create a new SalesOrderReleaseItem
                        var salesOrderReleaseItem = new SalesOrderReleaseItem(uow)
                        {
                            SalesOrderRelease = delivery.SalesOrderRelease,
                            SalesOrderItem = salesOrderItem,
                            QuantityToRelease = salesOrderItemParameter.QuantityToRelease,
                            Status = (quantityRemaining == 0 ? SalesOrderReleaseItemStatus.Released : SalesOrderReleaseItemStatus.PartReleased)
                        };
                        innerChanges.Add(new EntityChange(salesOrderReleaseItem, EntityChange.ChangeTypes.Add));

                        // Create a new DeliveryItem
                        var deliveryItem = new DeliveryItem(uow)
                        {
                            Delivery = delivery,
                            Sku = salesOrderItem.Sku,
                            SalesOrderReleaseItem = salesOrderReleaseItem,
                            QuantityReleased = 0,
                            CostPrice = salesOrderItem.CostPrice,
                            SellPrice = salesOrderItem.SellPrice
                        };
                        innerChanges.Add(new EntityChange(deliveryItem, EntityChange.ChangeTypes.Add));

                        // Link the PickItems to the DeliveryItem
                        foreach (var pickItem in pickItems)
                        {
                            pickItem.DeliveryItem = deliveryItem;
                        }

                        // Commit the item release
                        uow.CommitChanges();

                        // Record the changed entities
                        changes.AddRange(innerChanges);

                        return releaseErrorMessage;
                    }
                    catch (LockingException)
                    {
                        // Rollback the failed changes
                        uow.RollbackTransaction();

                        // If the stock was updated elsewhere, retry until attemptsRemaining = 0
                        if (--attemptsRemaining <= 0)
                        {
                            throw new ServiceMethodException(string.Format("Failed to release after {0} attempts.", MaxReleaseAttempts));
                        }

                        // Wait a small random delay before the next attempt
                        Thread.Sleep(new Random().Next(10, 100));
                    }
                }
            }
            catch (Exception ex)
            {
                // Rollback the failed changes
                uow.RollbackTransaction();

                // Create a SalesOrderReleaseItem containing the error message
                CreateFailedReleaseItem(ex.Message, salesOrderItemParameter, delivery.SalesOrderRelease, uow, changes);

                // If a new global error has been set, mark all prior release items as failed and reverse the released quantities
                if (!string.IsNullOrWhiteSpace(releaseErrorMessage))
                {
                    foreach (var deliveryItem in delivery.DeliveryItems)
                    {
                        deliveryItem.SalesOrderReleaseItem.Status = SalesOrderReleaseItemStatus.Failed;
                        deliveryItem.SalesOrderReleaseItem.ErrorMessage = releaseErrorMessage;
                        deliveryItem.SalesOrderReleaseItem.SalesOrderItem.QuantityReleased -= deliveryItem.Quantity;
                    }
                    uow.CommitChanges();
                }
            }

            return releaseErrorMessage;
        }

        /// <summary>
        /// Creates a failed SalesOrderReleaseItem containing an error message.
        /// </summary>
        /// <param name="errorMessage">The error message describing why the release failed.</param>
        /// <param name="salesOrderItemParameter">An object containing information about the SalesOrderItem to release.</param>
        /// <param name="salesOrderRelease">The SalesOrderRelease to add the release item to.</param>
        /// <param name="uow">The UnitOfWork for updating the data.</param>
        /// <param name="changes">A list of entity changes that have occurred.</param>
        private void CreateFailedReleaseItem(string errorMessage, SalesOrderItemParameter salesOrderItemParameter, SalesOrderRelease salesOrderRelease, UnitOfWork uow, List<EntityChange> changes)
        {
            // Get the related SalesOrderItem
            var salesOrderItem = uow.GetObjectByKey<SalesOrderItem>(salesOrderItemParameter.SalesOrderItemOid);

            // Create a SalesOrderReleaseItem containing the error message
            var salesOrderReleaseItem = new SalesOrderReleaseItem(uow)
            {
                SalesOrderRelease = salesOrderRelease,
                SalesOrderItem = salesOrderItem,
                QuantityToRelease = salesOrderItemParameter.QuantityToRelease,
                Status = SalesOrderReleaseItemStatus.Failed,
                ErrorMessage = errorMessage
            };

            // Commit the item release
            uow.CommitChanges();

            // Record the changed entities
            changes.Add(new EntityChange(salesOrderReleaseItem, EntityChange.ChangeTypes.Add));
        }

        #endregion
    }
}
