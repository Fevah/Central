using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Editor.Definition.Helper
{
    public class EnumEditorItem : BindableBase
    {
        #region Private Fields

        private string _text;
        private ImageSource _image;
        private string _toolTip;

        #endregion


        #region Constructors

        public EnumEditorItem(string text, string imageUri = null, string toolTip = null)
        {
            Text = text;
            ToolTip = toolTip;

            if (!string.IsNullOrWhiteSpace(imageUri))
            {
                var image = new BitmapImage(new Uri(imageUri, UriKind.Absolute));
                image.Freeze();
                Image = image;
            }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The text displayed by this item.
        /// </summary>
        public string Text
        {
            get { return _text; }
            set { SetProperty(ref _text, value, () => Text); }
        }

        /// <summary>
        /// The image displayed by this item.
        /// </summary>
        public ImageSource Image
        {
            get { return _image; }
            set { SetProperty(ref _image, value, () => Image); }
        }

        /// <summary>
        /// The tooltip displayed by this item.
        /// </summary>
        public string ToolTip
        {
            get { return _toolTip; }
            set { SetProperty(ref _toolTip, value, () => ToolTip); }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return Text;
        }

        #endregion
    }
}
