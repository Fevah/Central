using System;
using System.ComponentModel;
using System.Linq.Expressions;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Editor.DataModel
{
    /// <summary>
    /// This class is used to parse the string data stored in each comment into update time, the user id who create the comment and the content of the comment.
    /// </summary>
    [Serializable]
    public class CommentDataModel : INotifyPropertyChanged
    {
        #region Private Fields

        private string _content;
        private string _updateTime;
        private string _userId;

        #endregion


        #region Public Properties

        /// <summary>
        /// The content string of the comment.
        /// </summary>
        public string Content
        {
            get { return _content; }
            set
            {
                if (Equals(_content, value))
                    return;

                _content = value;
                RaisePropertyChanged(() => Content);
            }
        }

        /// <summary>
        /// The update time of the comment.
        /// </summary>
        public string UpdateTime
        {
            get { return _updateTime; }
            set
            {
                if (Equals(_updateTime, value))
                    return;

                _updateTime = value;
                RaisePropertyChanged(() => UpdateTime);
            }
        }

        /// <summary>
        /// The id of the user who wrote the comment.
        /// </summary>
        public string UserId
        {
            get { return _userId; }
            set
            {
                if (Equals(_userId, value))
                    return;

                _userId = value;
                RaisePropertyChanged(() => UserId);
            }
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
