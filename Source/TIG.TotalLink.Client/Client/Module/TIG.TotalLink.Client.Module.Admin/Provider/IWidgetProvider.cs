using System.Collections.ObjectModel;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Provider
{
    public interface IWidgetProvider
    {
        ObservableCollection<WidgetViewModel> Widgets { get; }
    }
}
