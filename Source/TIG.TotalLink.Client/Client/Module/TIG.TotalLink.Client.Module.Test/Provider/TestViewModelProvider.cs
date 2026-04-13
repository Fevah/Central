using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TIG.TotalLink.Client.Module.Test.ViewModel.Test;

namespace TIG.TotalLink.Client.Module.Test.Provider
{
    /// <summary>
    /// Provides a list of TestViewModels.
    /// </summary>
    public class TestViewModelProvider : ITestViewModelProvider
    {
        #region Constructors

        public TestViewModelProvider()
        {
            // Initialize collections
            Items = new ObservableCollection<TestViewModel>();

            // Initialize the list of available items
            Task.Run(() => InitializeItems());
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// All available items.
        /// </summary>
        public ObservableCollection<TestViewModel> Items { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Generates a list of items.
        /// </summary>
        private void InitializeItems()
        {
            for (var i = 0; i < 10; i++)
            {
                var i1 = i;
                Application.Current.Dispatcher.Invoke(() => Items.Add(new TestViewModel() { Name = string.Format("Test {0:00}", i1) }));
            }
        }

        #endregion
    }
}
