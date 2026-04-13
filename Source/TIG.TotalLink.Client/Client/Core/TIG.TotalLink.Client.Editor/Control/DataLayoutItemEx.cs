using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Data;
using DevExpress.Mvvm;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class DataLayoutItemEx : DataLayoutItem, INotifyPropertyChanged
    {
        #region Private Fields

        private DataLayoutItemWrapper _editorWrapper;

        #endregion


        #region Dependency Properties

        public static readonly DependencyProperty EditValuePropertyNameProperty = DependencyProperty.RegisterAttached("EditValuePropertyName", typeof(string), typeof(DataLayoutItemEx), new PropertyMetadata("EditValue"));
        public static readonly DependencyProperty EditValueConverterProperty = DependencyProperty.RegisterAttached("EditValueConverter", typeof(IValueConverter), typeof(DataLayoutItemEx));
        public static readonly DependencyProperty EditValueConverterParameterProperty = DependencyProperty.RegisterAttached("EditValueConverterParameter", typeof(object), typeof(DataLayoutItemEx));

        /// <summary>
        /// The name of the property that the editors value should be bound to.
        /// Defaults to "EditValue".
        /// </summary>
        public string EditValuePropertyName
        {
            get { return (string)GetValue(EditValuePropertyNameProperty); }
            set { SetValue(EditValuePropertyNameProperty, value); }
        }

        /// <summary>
        /// An IValueConverter to apply to the EditValue binding.
        /// </summary>
        public IValueConverter EditValueConverter
        {
            get { return (IValueConverter)GetValue(EditValueConverterProperty); }
            set { SetValue(EditValueConverterProperty, value); }
        }

        /// <summary>
        /// A parameter value to be passed to the EditValueConverter.
        /// </summary>
        public object EditValueConverterParameter
        {
            get { return GetValue(EditValueConverterParameterProperty); }
            set { SetValue(EditValueConverterParameterProperty, value); }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The editor wrapper that this DataLayoutItem represents.
        /// </summary>
        public DataLayoutItemWrapper EditorWrapper
        {
            get { return _editorWrapper; }
            set
            {
                if (Equals(_editorWrapper, value))
                    return;

                _editorWrapper = value;
                RaisePropertyChanged(() => EditorWrapper);
            }
        }

        #endregion


        #region Overrides

        protected override bool GetIsActuallyReadOnly()
        {
            if (PropertyInfo == null || DataLayoutControl != null && DataLayoutControl.IsReadOnly || IsReadOnly)
                return true;

            return false;
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
