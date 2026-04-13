using TIG.TotalLink.Client.Module.Global.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Widget
{
    public class MainDatabaseViewModel : DatabaseConfigViewModelBase
    {
        #region Constructors

        public MainDatabaseViewModel()
        {
        }

        public MainDatabaseViewModel(IGlobalFacade globalFacade)
            : base(DatabaseDomain.Main, globalFacade)
        {
        }

        #endregion
    }
}
