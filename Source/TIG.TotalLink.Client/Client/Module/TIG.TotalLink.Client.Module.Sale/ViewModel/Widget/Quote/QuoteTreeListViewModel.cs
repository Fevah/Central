using TIG.TotalLink.Client.Facade.Sale.Facade.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.MVVM.Helper;
using TIG.TotalLink.Client.MVVM.Provider.Interface;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote
{
    public class QuoteTreeListViewModel : TreeListViewModelBase<Facade.Sale.Service.Quote>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public QuoteTreeListViewModel() { }

        /// <summary>
        /// Constructor with sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        /// <param name="entityTypeProvider">A provider to help with tracking entity set changes.</param>
        public QuoteTreeListViewModel(ISaleFacade saleFacade, IEntityTypeProvider entityTypeProvider)
            : base(entityTypeProvider)
        {
            // Store services.
            _saleFacade = saleFacade;

            // Initialize data sources.
            ItemsSource = DataSourceHelper.CreateTreeListInstantFeedbackDataSource(_saleFacade.Context, _saleFacade.Context.Quote);
        }

        #endregion
    }
}
