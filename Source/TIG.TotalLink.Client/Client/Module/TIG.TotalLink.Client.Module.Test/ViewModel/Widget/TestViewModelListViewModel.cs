using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Test.Provider;
using TIG.TotalLink.Client.Module.Test.ViewModel.Test;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Widget
{
    public class TestViewModelListViewModel : ListViewModelBase<TestViewModel>
    {
        #region Private Fields

        private readonly ITestViewModelProvider _testViewModelProvider;

        #endregion


        #region Constructors

        public TestViewModelListViewModel()
        {
        }

        public TestViewModelListViewModel(ITestViewModelProvider testViewModelProvider)
            : this()
        {
            // Store services
            _testViewModelProvider = testViewModelProvider;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Initialize the data source
                ItemsSource = _testViewModelProvider.Items;
            });
        }

        #endregion
    }
}
