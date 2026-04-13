using System.Threading.Tasks;
using TIG.TotalLink.Shared.Contract.Admin;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Admin
{
    [Facade(1, "Main", 4, "Admin")]
    public class AdminFacade : FacadeBase<RibbonCategory, IAdminMethodService>, IAdminFacade
    {
        #region Constructors

        public AdminFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the next Sequence Number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Code, Sequence Number and an array of changes that occurred.</returns>
        public GetSequenceNumberResult GetNextSequenceNumber(string sequenceName)
        {
            return MethodFacade.Execute(c => c.GetSequenceNumber(sequenceName));
        }

        /// <summary>
        /// Gets the next Sequence Number, and increments the Sequence by one.
        /// </summary>
        /// <param name="sequenceName">The name of the Sequence to use.</param>
        /// <returns>A GetSequenceNumberResult containing the Sequence Number and an array of changes that occurred.</returns>
        public async Task<GetSequenceNumberResult> GetNextSequenceNumberAsync(string sequenceName)
        {
            return await MethodFacade.ExecuteAsync(c => c.GetSequenceNumber(sequenceName)).ConfigureAwait(false);
        }

        #endregion
    }
}
