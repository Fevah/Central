using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.ActiveDirectory;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.ActiveDirectory
{
    [Facade(3, "ActiveDirectory")]
    public class ActiveDirectoryFacade : FacadeBase<ActiveDirectoryUser, IMethodServiceBase>, IActiveDirectoryFacade
    {
        #region Constructors

        public ActiveDirectoryFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion
    }
}