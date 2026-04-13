using System;
using System.Threading.Tasks;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Core.Interface;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client.Module.Admin.Extension
{
    public static class ReferenceDataObjectExtension
    {
        #region Public Methods

        /// <summary>
        /// Generates and stores a new Reference Number on an object which implements IReferenceDataObject.
        /// </summary>
        /// <param name="referenceDataObject">The IReferenceDataObject to store the Reference Number on.</param>
        public static void GenerateReferenceNumber(this IReferenceDataObject referenceDataObject)
        {
            try
            {
                // Attempt to get the AdminFacade
                var adminFacade = DataObjectHelper.GetFacade<Sequence>() as IAdminFacade;
                if (adminFacade == null)
                    throw new Exception("Failed to find facade to generate a Reference Number");

                // Ensure the Admin method service is connected
                adminFacade.Connect(ServiceTypes.Method);

                // Generate and store the new reference number
                var result = adminFacade.GetNextSequenceNumber(referenceDataObject.GetType().Name);
                referenceDataObject.Reference = ReferenceNumberHelper.FormatValue(AppContextViewModel.Instance.SystemCode, result.SequenceCode, result.SequenceNumber, AppContextViewModel.Instance.ReferenceValueFormat);

                // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
                adminFacade.NotifyDirtyTypes(result.EntityChanges);

                // Notify widgets of entity changes
                EntityChangedMessage.Send(null, result.EntityChanges);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                AppContextViewModel.Instance.GetMessageBoxService().Show(string.Format("Failed to generate Reference Number!\r\n\r\n{0}", serviceException.Message), "Unhandled error", MessageButton.OK, MessageIcon.Error, MessageResult.OK);
            }
        }

        /// <summary>
        /// Asynchronously generates and stores a new Reference Number on an object which implements IReferenceDataObject.
        /// </summary>
        /// <param name="referenceDataObject">The IReferenceDataObject to store the Reference Number on.</param>
        public async static Task GenerateReferenceNumberAsync(this IReferenceDataObject referenceDataObject)
        {
            try
            {
                // Attempt to get the AdminFacade
                var adminFacade = DataObjectHelper.GetFacade<Sequence>() as IAdminFacade;
                if (adminFacade == null)
                    throw new Exception("Failed to find facade to generate a Reference Number");

                // Ensure the Admin method service is connected
                adminFacade.Connect(ServiceTypes.Method);

                // Generate and store the new reference number
                var result = await adminFacade.GetNextSequenceNumberAsync(referenceDataObject.GetType().Name).ConfigureAwait(false);
                referenceDataObject.Reference = ReferenceNumberHelper.FormatValue(AppContextViewModel.Instance.SystemCode, result.SequenceCode, result.SequenceNumber, AppContextViewModel.Instance.ReferenceValueFormat);

                // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
                adminFacade.NotifyDirtyTypes(result.EntityChanges);

                // Notify widgets of entity changes
                EntityChangedMessage.Send(null, result.EntityChanges);
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                AppContextViewModel.Instance.GetMessageBoxService().Show(string.Format("Failed to generate Reference Number!\r\n\r\n{0}", serviceException.Message), "Unhandled error", MessageButton.OK, MessageIcon.Error, MessageResult.OK);
            }
        }

        #endregion
    }
}
