using DevExpress.Mvvm;
using MonitoredUndo;
using TIG.TotalLink.Client.Undo.Core;

namespace TIG.TotalLink.Client.Undo.AppContext
{
    public class AppUndoRootViewModel : ViewModelBase, ISupportsUndo
    {
        #region Static Instance

        private static AppUndoRootViewModel _instance;

        public static AppUndoRootViewModel Instance
        {
            get { return _instance ?? (_instance = new AppUndoRootViewModel()); }
        }

        #endregion


        #region Private Fields

        private UndoRoot _undoRoot;
        private int _trackUndoCount;

        #endregion


        //#region Constructors

        //public AppUndoRootViewModel()
        //{
        //    // Migrate settings if required
        //    UpgradeSettings();
        //}

        //#endregion


        #region Public Properties

        /// <summary>
        /// The default change factory.
        /// </summary>
        public ChangeFactoryEx ChangeFactory
        {
            get { return (ChangeFactoryEx)DefaultChangeFactory.Current; }
        }

        ///// <summary>
        ///// The capacity of the undo stack.
        ///// </summary>
        //public int UndoStackCapacity
        //{
        //    get { return Settings.Default.UndoStackCapacity; }
        //    set
        //    {
        //        var undoStackCapacity = Settings.Default.UndoStackCapacity;
        //        SetProperty(ref undoStackCapacity, value, () => UndoStackCapacity, () =>
        //            {
        //                Settings.Default.UndoStackCapacity = value;
        //                Settings.Default.Save();
        //            }
        //        );
        //    }
        //}
        
        // TODO : Store UndoStackCapacity in user settings
        /// <summary>
        /// The capacity of the undo stack.
        /// </summary>
        public int UndoStackCapacity
        {
            get { return 5; }
        }

        /// <summary>
        /// The root object for storing application level undo/redo changes.
        /// </summary>
        public UndoRoot UndoRoot
        {
            get
            {
                if (_undoRoot == null)
                    _undoRoot = UndoService.Current[GetUndoRoot()];

                return _undoRoot;
            }
        }

        /// <summary>
        /// Indicates if actions will be tracked on the undo stack.
        /// Repeated calls of 'TrackUndo = false' will be counted, and tracking will only be turned back on after an equal number of calls of 'TrackUndo = true'.
        /// </summary>
        public bool TrackUndo
        {
            get { return ChangeFactory.TrackUndo; }
            set
            {
                if (value)
                {
                    if (--_trackUndoCount <= 0)
                    {
                        _trackUndoCount = 0;
                        ChangeFactory.TrackUndo = true;
                    }
                }
                else
                {
                    ChangeFactory.TrackUndo = false;
                    _trackUndoCount++;
                }
            }
        }

        #endregion


        #region Private Methods

        ///// <summary>
        ///// Migrates settings from a previous version of the application if required.
        ///// </summary>
        //private void UpgradeSettings()
        //{
        //    // Abort if no upgrade is required
        //    if (!Settings.Default.CallUpgrade)
        //        return;

        //    // Migrate settings
        //    Settings.Default.Upgrade();
        //    Settings.Default.CallUpgrade = false;
        //    Settings.Default.Save();
        //}

        #endregion


        #region ISupportsUndo

        public object GetUndoRoot()
        {
            return Instance;
        }

        #endregion
    }
}
