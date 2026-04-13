using System.Threading.Tasks;
using TIG.TotalLink.Shared.Contract.Admin;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Admin
{
    public interface IAdminFacade : IFacadeBase
    {
        #region Public Methods

        /// <summary>
        /// Gets the next Sequence Number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Code, Sequence Number and an array of changes that occurred.</returns>
        GetSequenceNumberResult GetNextSequenceNumber(string sequenceName);

        /// <summary>
        /// Asynchronously gets the next Sequence number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Code, Sequence Number and an array of changes that occurred.</returns>
        Task<GetSequenceNumberResult> GetNextSequenceNumberAsync(string sequenceName);

        #endregion
    }
}
