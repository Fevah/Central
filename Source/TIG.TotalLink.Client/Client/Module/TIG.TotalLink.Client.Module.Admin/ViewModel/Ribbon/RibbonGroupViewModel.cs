using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon
{
    public class RibbonGroupViewModel : EntityViewModelBase<RibbonGroup>
    {
        #region Private Fields

        private readonly ObservableCollection<RibbonItemViewModelBase> _ribbonItems = new ObservableCollection<RibbonItemViewModelBase>();
        private ICommand _captionButtonCommand;

        #endregion


        #region Constructors

        public RibbonGroupViewModel()
        {
        }

        public RibbonGroupViewModel(RibbonGroup dataObject)
            : this()
        {
            // Initialize the group
            DataObject = dataObject;
            Mapper.Map(dataObject.RibbonItems, RibbonItems);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the group.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        /// <summary>
        /// Indicates if the group should sow the caption button.
        /// </summary>
        public bool ShowCaptionButton
        {
            get { return DataObject.ShowCaptionButton; }
        }

        /// <summary>
        /// Child items of this group.
        /// </summary>
        [AssignParentViewModel]
        [SyncFromDataObject]
        public ObservableCollection<RibbonItemViewModelBase> RibbonItems
        {
            get { return _ribbonItems; }
        }

        /// <summary>
        /// The command to execute when the caption button is pressed.
        /// </summary>
        public ICommand CaptionButtonCommand
        {
            get { return _captionButtonCommand; }
            set { SetProperty(ref _captionButtonCommand, value, () => CaptionButtonCommand); }
        }

        #endregion
    }
}
