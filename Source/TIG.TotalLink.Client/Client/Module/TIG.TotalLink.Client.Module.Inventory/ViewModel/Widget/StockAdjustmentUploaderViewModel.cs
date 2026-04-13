using System;
using System.Collections.Generic;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Inventory.Uploader;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class StockAdjustmentUploaderViewModel : UploaderViewModelBase<StockAdjustmentUploaderDataModel>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;
        private UnitOfWork _unitOfWork;
        private List<EntityChange> _allChanges;

        #endregion


        #region Constructors

        public StockAdjustmentUploaderViewModel()
        {
        }

        public StockAdjustmentUploaderViewModel(IInventoryFacade inventoryFacade)
            : this()
        {
            _inventoryFacade = inventoryFacade;

            // We cannot apply Stock Adjustments in a batch because the changes are written at the server end
            UploadBatchSize = 1;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Creates a StockAdjustment.
        /// </summary>
        /// <param name="dataModel">The data model containing source values.</param>
        private void CreateStockAdjustment(StockAdjustmentUploaderDataModel dataModel)
        {
            // Create a temporary StockAdjustment
            // This StockAdjustment will never be saved, but it must exist in a connected UnitOfWork so that LookUps can be resolved
            var stockAdjustment = new StockAdjustment(_unitOfWork)
            {
                Reason = _unitOfWork.GetDataObject(dataModel.AdjustmentReason),
                Vendor = _unitOfWork.GetDataObject(dataModel.Vendor),
                VendorReference = dataModel.ConNote,
                Sku = _unitOfWork.GetDataObject(dataModel.Sku),
                Quantity = dataModel.Quantity,
                TargetBinLocation = _unitOfWork.GetDataObject(dataModel.TargetBin),
                TargetPhysicalStockType = _unitOfWork.GetDataObject(dataModel.TargetStockType),
                SourceBinLocation = _unitOfWork.GetDataObject(dataModel.SourceBin),
                SourcePhysicalStockType = _unitOfWork.GetDataObject(dataModel.SourceStockType),
                Notes = dataModel.Notes
            };

            try
            {
                // Add the StockAdjustment
                var changes = _inventoryFacade.AddStockAdjustment(stockAdjustment);

                // Add the changes that occurred to the master list
                _allChanges.AddRange(changes);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                throw new Exception(serviceException.Message);
            }
        }

        #endregion


        #region Overrides

        protected override void InitializeUpload()
        {
            base.InitializeUpload();

            // Create a UnitOfWork to contain temporary StockAdjustments
            _unitOfWork = _inventoryFacade.CreateUnitOfWork();

            // Create a list to track all changed entities
            _allChanges = new List<EntityChange>();
        }

        protected override void UploadRow(StockAdjustmentUploaderDataModel dataModel)
        {
            CreateStockAdjustment(dataModel);
        }

        protected override void WriteBatch()
        {
            base.WriteBatch();

            // Throw away temporary StockAdjustments
            _unitOfWork.DropChanges();
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

            var allChangesArray = _allChanges.ToArray();

            // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
            _inventoryFacade.NotifyDirtyTypes(allChangesArray);

            // Notify other widgets of the changed entities
            EntityChangedMessage.Send(this, allChangesArray);
        }

        #endregion
    }
}