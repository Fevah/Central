using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Integration;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Integration
{
    [Facade(1, "Main")]
    public class IntegrationFacade : FacadeBase<SyncEntity, IMethodServiceBase>, IIntegrationFacade
    {
        #region Constructors

        public IntegrationFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}
