using System;
using System.Windows.Input;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.Command;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core
{
    public abstract class RibbonItemViewModelBase : EntityViewModelBase<RibbonItem>
    {
        #region Private Fields

        protected ICommand _command;
        private object _commandParameter;

        #endregion


        #region Constructors

        protected RibbonItemViewModelBase()
        {
        }

        protected RibbonItemViewModelBase(RibbonItem dataObject)
            : this()
        {
            // Initialize the item
            DataObject = dataObject;
            RefreshCommand();

            // Initialize event handlers
            DataObject.Changed += DataObject_Changed;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the item.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        /// <summary>
        /// The description of the item.
        /// </summary>
        public string Description
        {
            get { return DataObject.Description; }
        }

        /// <summary>
        /// Command that will be executed when the button is clicked.
        /// </summary>
        public ICommand Command
        {
            get { return _command; }
            set { SetProperty(ref _command, value, () => Command); }
        }

        /// <summary>
        /// Parameter value that will be passed to the command.
        /// If this parameter is set on the viewmodel, it will override the value on the datamodel.
        /// </summary>
        public object CommandParameter
        {
            get { return (_commandParameter ?? (DataObject != null ? DataObject.CommandParameter : null)); }
            set { SetProperty(ref _commandParameter, value, () => CommandParameter); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Generates a Command based on the CommandType and CommandParameter from the data object.
        /// </summary>
        public void RefreshCommand()
        {
            // Abort if the DataModel is null
            if (DataObject == null)
                return;

            switch (DataObject.CommandType)
            {
                case CommandType.ShowDocument:
                    Guid documentId;
                    if (Guid.TryParse(DataObject.CommandParameter, out documentId))
                    {
                        // If the parameter contains a guid, create a command to open the specified document
                        Command = new ShowDocumentCommand(documentId);
                    }
                    else
                    {
                        // If the parameter contained a string, create a command to open a new document
                        Command = new ShowDocumentCommand(DataObject.CommandParameter);
                    }
                    break;

                case CommandType.WidgetCommand:
                    // When the CommandType is WidgetCommand we will not generate the command here
                    // Instead it will be assigned directly by the WidgetViewModelBase
                    break;
            }
        }

        #endregion


        #region Event Handlers
        
        private void DataObject_Changed(object sender, ObjectChangeEventArgs e)
        {
            // If any of the properties that define the command are changed, then we need to refresh the command
            if (e.PropertyName == "CommandType" || e.PropertyName == "CommandParameter")
                RefreshCommand();
        }

        #endregion
    }
}
