using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using Microsoft.Win32;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition;

namespace TIG.TotalLink.Client.Editor.View
{
    /// <summary>
    /// Interaction logic for UploadEdit.xaml
    /// </summary>
    public partial class UploadEdit : INotifyPropertyChanged
    {
        public static readonly DependencyProperty EditValueProperty = DependencyProperty.Register(
            "EditValue", typeof(ObservableCollection<string>), typeof(UploadEdit), new PropertyMetadata(null));

        public static readonly DependencyProperty EditorDefinitionProperty = DependencyProperty.RegisterAttached(
            "EditorDefinition", typeof(EditorDefinitionBase), typeof(UploadEdit));

        /// <summary>
        /// Describes the configuration for this UploadEdit.
        /// </summary>
        public EditorDefinitionBase EditorDefinition
        {
            get { return (EditorDefinitionBase)GetValue(EditorDefinitionProperty); }
            set { SetValue(EditorDefinitionProperty, value); }
        }

        /// <summary>
        /// Items sources for upload items
        /// </summary>
        public ObservableCollection<string> EditValue
        {
            get { return (ObservableCollection<string>)GetValue(EditValueProperty); }
            set { SetValue(EditValueProperty, value); }
        }

        /// <summary>
        /// Handles the Loaded event for the UploadEdit.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UploadEdit_Loaded(object sender, RoutedEventArgs e)
        {
            if (EditValue == null)
            {
                ItemsSource = new ObservableCollection<string>();
                SetValue(EditValueProperty, ItemsSource);
                return;
            }

            ItemsSource = EditValue;
        }

        #region Private Properties

        private ICommand _addCommand;
        private ICommand _deleteCommand;
        private ICommand _clearCommand;
        private ICommand _dropCommand;
        private ObservableCollection<string> _itemsSource;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public UploadEdit()
        {
            InitializeComponent();

            AddCommand = new DelegateCommand(OnAddExecute);
            DeleteCommand = new DelegateCommand<string>(OnDeleteExecute);
            DropCommand = new DelegateCommand<DragEventArgs>(OnDropExecute);
            ClearCommand = new DelegateCommand(OnClearExecute, OnClearCanExecute);

            Loaded += UploadEdit_Loaded;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// CanExecute method for the ClearCommand.
        /// </summary>
        private bool OnClearCanExecute()
        {
            return ItemsSource != null && ItemsSource.Any();
        }

        private void OnClearExecute()
        {
            ItemsSource.Clear();
            SetValue(EditValueProperty, ItemsSource);
        }

        private void OnDropExecute(DragEventArgs e)
        {
            var files = (FileInfo[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                ItemsSource.Add(file.Name);
            }
            SetValue(EditValueProperty, ItemsSource);
        }

        private void OnDeleteExecute(string fileInfo)
        {
            ItemsSource.Remove(fileInfo);
            SetValue(EditValueProperty, ItemsSource);
        }

        private void OnAddExecute()
        {
            var ofd = new OpenFileDialog { Multiselect = true };

            try
            {
                var defination = EditorDefinition as UploadEditorDefinition;

                //Check the file filter (filter is used to filter file extensions to select, for example only .jpg files)
                ofd.Filter = defination != null && !string.IsNullOrEmpty(defination.FileFilter) ? defination.FileFilter : "*";
            }
            catch (ArgumentException ex)
            {
                //User supplied a wrong configuration file
                throw new Exception("Wrong file filter configuration.", ex);
            }

            if (ofd.ShowDialog() != true)
                return;

            foreach (var fileName in ofd.FileNames.Where(fileName => !EditValue.Contains(fileName)))
            {
                ItemsSource.Add(fileName);
            }

            SetValue(EditValueProperty, ItemsSource);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        public ICommand AddCommand
        {
            get { return _addCommand; }
            set
            {
                _addCommand = value;
                RaisePropertyChanged(() => AddCommand);
            }
        }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        public ICommand DeleteCommand
        {
            get { return _deleteCommand; }
            set
            {
                _deleteCommand = value;
                RaisePropertyChanged(() => DeleteCommand);
            }
        }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        public ICommand ClearCommand
        {
            get { return _clearCommand; }
            set
            {
                _clearCommand = value;
                RaisePropertyChanged(() => ClearCommand);
            }
        }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        public ICommand DropCommand
        {
            get { return _dropCommand; }
            set
            {
                _dropCommand = value;
                RaisePropertyChanged(() => DropCommand);
            }
        }

        /// <summary>
        /// ItemsSource for select upload item
        /// </summary>
        public ObservableCollection<string> ItemsSource
        {
            get { return _itemsSource; }
            set
            {
                _itemsSource = value;
                RaisePropertyChanged(() => ItemsSource);
            }
        }

        #endregion


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged<T>(Expression<Func<T>> expression)
        {
            var changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, new PropertyChangedEventArgs(BindableBase.GetPropertyName(expression)));
        }

        #endregion
    }
}
