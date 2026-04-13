using System.Collections.ObjectModel;
using TIG.TotalLink.Client.Module.Test.ViewModel.Test;

namespace TIG.TotalLink.Client.Module.Test.Provider
{
    public interface ITestViewModelProvider
    {
        ObservableCollection<TestViewModel> Items { get; }
    }
}
