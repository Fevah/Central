using System;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.Contract.Admin;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Server.MethodService.Admin
{
    public class AdminMethodService : MethodServiceBase, IAdminMethodService
    {
        #region Private Constants

        private const int MaxSequenceGenerationAttempts = 10;

        #endregion


        #region Private Fields

        private static IAdminFacade _adminFacade;

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the next Sequence number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Code, Sequence Number and an array of changes that occurred.</returns>
        public GetSequenceNumberResult GetSequenceNumber(string sequenceName)
        {
            var result = new GetSequenceNumberResult();

            // Get the AdminFacade
            var adminFacade = GetAdminDataFacade();

            try
            {
                using (var uow = adminFacade.CreateUnitOfWork())
                {
                    var attemptsRemaining = MaxSequenceGenerationAttempts;

                    while (true)
                    {
                        try
                        {
                            // To make sure we get up-to-date data, flag the Sequence table as dirty
                            adminFacade.NotifyDirtyTypes(typeof(Sequence));

                            // Attempt to get the requested sequence
                            var sequence = uow.Query<Sequence>().FirstOrDefault(s => s.Name == sequenceName);
                            if (sequence == null)
                                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to find a sequence named '{0}'!", sequenceName)));

                            // Store and increment the sequence number
                            result.SequenceCode = sequence.Code;
                            result.SequenceNumber = sequence.NextNumber;
                            sequence.NextNumber++;

                            // Commit changes
                            uow.CommitChanges();

                            // Record the changed entities
                            result.EntityChanges = new[] { new EntityChange(sequence, EntityChange.ChangeTypes.Modify) };

                            // Return the result
                            return result;
                        }
                        catch (LockingException)
                        {
                            // Rollback the failed changes
                            uow.RollbackTransaction();

                            // If the sequence was updated elsewhere, retry until attemptsRemaining = 0
                            if (--attemptsRemaining <= 0)
                                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to get a sequence number for '{0}' after {1} attempts!", sequenceName, MaxSequenceGenerationAttempts)));

                            // Wait a small random delay before the next attempt
                            Thread.Sleep(new Random().Next(10, 100));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to get a sequence number for '{0}'!", sequenceName), ex));
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns an AdminFacade connected to the data service only.
        /// </summary>
        /// <returns>An AdminFacade.</returns>
        private static IAdminFacade GetAdminDataFacade()
        {
            if (_adminFacade == null)
                _adminFacade = new AdminFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_adminFacade != null && !_adminFacade.IsDataConnected)
                    _adminFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Admin Facade!", ex));
            }

            return _adminFacade;
        }

        #endregion
    }
}
