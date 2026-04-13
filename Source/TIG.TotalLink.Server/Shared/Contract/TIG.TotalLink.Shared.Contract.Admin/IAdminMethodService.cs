using System.ServiceModel;
using TIG.TotalLink.Shared.Contract.Core;

namespace TIG.TotalLink.Shared.Contract.Admin
{
    [ServiceContract]
    public interface IAdminMethodService : IMethodServiceBase
    {
        #region Public Methods

        /// <summary>
        /// Gets the next Sequence Number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Code, Sequence Number and an array of changes that occurred.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        GetSequenceNumberResult GetSequenceNumber(string sequenceName);

        #endregion
    }
}
