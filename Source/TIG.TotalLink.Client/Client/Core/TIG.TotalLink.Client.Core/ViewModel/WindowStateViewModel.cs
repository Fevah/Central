using System.Windows;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Core.ViewModel
{
    public class WindowStateViewModel : ViewModelBase
    {
        #region Private Fields

        private readonly string _windowName;
        private WindowState _state = WindowState.Normal;
        private double _left;
        private double _top;
        private double _width;
        private double _height;
        private WindowState _actualState = WindowState.Normal;
        private double _actualLeft;
        private double _actualTop;
        private double _actualWidth;
        private double _actualHeight;

        #endregion


        #region Constructors

        public WindowStateViewModel(string windowName)
        {
            // Cache the window name
            _windowName = windowName;
        }

        public WindowStateViewModel(string windowName, double defaultWidth, double defaultHeight)
            : this(windowName)
        {
            // Initialize the window settings
            ActualWidth = defaultWidth;
            ActualHeight = defaultHeight;
            SetCenter();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the window.
        /// </summary>
        public string WindowName
        {
            get { return _windowName; }
        }

        /// <summary>
        /// The state of the window that will be saved.
        /// </summary>
        public WindowState State
        {
            get { return _state; }
            private set { SetProperty(ref _state, value, () => State); }
        }

        /// <summary>
        /// The actual state of the window.
        /// </summary>
        public WindowState ActualState
        {
            get { return _actualState; }
            set
            {
                SetProperty(ref _actualState, value, () => ActualState, () =>
                {
                    State = (_actualState == WindowState.Minimized ? WindowState.Normal : _actualState);
                });
            }
        }

        /// <summary>
        /// The left margin of the window that will be saved.
        /// </summary>
        public double Left
        {
            get { return _left; }
            set { SetProperty(ref _left, value, () => Left); }
        }

        /// <summary>
        /// The actual left margin of the window.
        /// </summary>
        public double ActualLeft
        {
            get { return _actualLeft; }
            set
            {
                SetProperty(ref _actualLeft, value, () => ActualLeft, () =>
                {
                    if (_actualState == WindowState.Normal)
                        Left = _actualLeft;
                });
            }
        }

        /// <summary>
        /// The top margin of the window that will be saved.
        /// </summary>
        public double Top
        {
            get { return _top; }
            set { SetProperty(ref _top, value, () => Top); }
        }

        /// <summary>
        /// The actual top margin of the window.
        /// </summary>
        public double ActualTop
        {
            get { return _actualTop; }
            set
            {
                SetProperty(ref _actualTop, value, () => ActualTop, () =>
                {
                    if (_actualState == WindowState.Normal)
                        Top = _actualTop;
                });
            }
        }

        /// <summary>
        /// The width of the window that will be saved.
        /// </summary>
        public double Width
        {
            get { return _width; }
            set { SetProperty(ref _width, value, () => Width); }
        }

        /// <summary>
        /// The actual width of the window.
        /// </summary>
        public double ActualWidth
        {
            get { return _actualWidth; }
            set
            {
                SetProperty(ref _actualWidth, value, () => ActualWidth, () =>
                {
                    if (_actualState == WindowState.Normal)
                        Width = _actualWidth;
                });
            }
        }

        /// <summary>
        /// The height of the window that will be saved.
        /// </summary>
        public double Height
        {
            get { return _height; }
            set { SetProperty(ref _height, value, () => Height); }
        }

        /// <summary>
        /// The actual height of the window.
        /// </summary>
        public double ActualHeight
        {
            get { return _actualHeight; }
            set
            {
                SetProperty(ref _actualHeight, value, () => ActualHeight, () =>
                {
                    if (_actualState == WindowState.Normal)
                        Height = _actualHeight;
                });
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Set the location of the window as the center of the primary screen.
        /// </summary>
        private void SetCenter()
        {
            // TODO : Screen width and height should be collected from active monitor, not primary monitor
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            ActualLeft = (screenWidth - Width) / 2;
            ActualTop = (screenHeight - Height) / 2;
        }

        #endregion
    }
}
