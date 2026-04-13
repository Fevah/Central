using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Editor.DataModel;

namespace TIG.TotalLink.Client.Editor.View
{
    /// <summary>
    /// The user control is used to add comment in a listbox with RichTextEdit as list item.
    /// </summary>
    public partial class CommentEdit : INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty EditValueProperty = DependencyProperty.Register("EditValue", typeof(string), typeof(CommentEdit),
            new FrameworkPropertyMetadata
            {
                BindsTwoWayByDefault = true,
                DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                PropertyChangedCallback = EditValueChangedCallBack
            });

        /// <summary>
        /// Store the string value of all comments.
        /// </summary>
        public string EditValue
        {
            get { return (string)GetValue(EditValueProperty); }
            set { SetValue(EditValueProperty, value); }
        }

        #endregion


        #region Private Fields

        private ObservableCollection<CommentDataModel> _dataList;

        #endregion


        #region Constructors

        public CommentEdit()
        {
            InitializeComponent();
            SaveCommentCommand = new DelegateCommand(OnSaveCommentCommandExecute);
            CancelCommentCommand = new DelegateCommand(OnCancelCommentCommandExecute);
            SetDataList();
        }

        #endregion


        #region Commands

        /// <summary>
        /// The command is used for binding commands in the save button in RichTextEdit.
        /// </summary>
        public ICommand SaveCommentCommand { get; private set; }

        /// <summary>
        /// The command is used for binding commands in the cancel button in RichTextEdit.
        /// </summary>
        public ICommand CancelCommentCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The data shown in the list. 
        /// </summary>
        public ObservableCollection<CommentDataModel> DataList
        {
            get { return _dataList; }
            set
            {
                _dataList = value;
                RaisePropertyChanged(() => DataList);
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the SaveCommentCommand.
        /// </summary>
        public void OnSaveCommentCommandExecute()
        {
            if (string.IsNullOrEmpty(DataList[0].Content))
            {
                // If the first comment is empty, remove it
                DataList.RemoveAt(0);
            }
            else
            {
                // Update the DateTime and user
                DataList[0].UpdateTime = DateTime.Now.ToUniversalTime().ToString("u");
                DataList[0].UserId = (AppContextViewModel.Instance.UserInfo != null ? AppContextViewModel.Instance.UserInfo.UserName : null);
            }

            // Update the EditValue
            EditValue = JsonConvert.SerializeObject(DataList);
        }

        /// <summary>
        /// Execute method for the CancelCommentCommand.
        /// </summary>
        public void OnCancelCommentCommandExecute()
        {
            SetDataList();
        }

        /// <summary>
        /// The callback when the edit value changed.
        /// </summary>
        /// <param name="d">The comment edit whose EditValue has changed.</param>
        /// <param name="e">Dependency property changed event arguments.</param>
        private static void EditValueChangedCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Attempt to get the DependencyObject as a CommentEdit
            var commentEdit = d as CommentEdit;
            if (commentEdit == null)
                return;

            // Initialize the data list
            commentEdit.SetDataList();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Set the data list based on the edit value.
        /// </summary>
        public void SetDataList()
        {
            // Create a new data list
            var newDataList = new ObservableCollection<CommentDataModel>();

            // Create a dummy entry to allow adding new comments
            var newComment = new CommentDataModel { UserId = "Add a comment..." };

            if (string.IsNullOrEmpty(EditValue))
            {
                // If there is no comments, just add a dummy entry
                newDataList.Add(newComment);
            }
            else
            {
                // Otherwise, deserialize the comments into the list, and then insert the dummy entry at the beginning
                newDataList = JsonConvert.DeserializeObject<ObservableCollection<CommentDataModel>>(EditValue);
                newDataList.Insert(0, newComment);
            }

            // Assign the new data list
            DataList = newDataList;

            // Clear the selected item
            CommentList.SelectedItem = null;
        }

        #endregion


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged<T1>(Expression<Func<T1>> expression)
        {
            var changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, new PropertyChangedEventArgs(BindableBase.GetPropertyName(expression)));
        }

        #endregion
    }
}