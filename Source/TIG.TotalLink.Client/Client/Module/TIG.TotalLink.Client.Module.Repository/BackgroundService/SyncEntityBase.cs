using System;
using System.Threading;
using System.Windows;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.BackgroundService;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;

namespace TIG.TotalLink.Client.Module.Repository.BackgroundService
{
    [NonPersistent]
    public class SyncEntityBase : LocalDataObjectBase, ISyncEntity
    {
        #region Private Properties

        private long _fileSize;
        private long _progress;
        private string _errorMessage;
        private SyncMode _mode;
        private SyncStatus _status;

        #endregion


        #region Protected Properties

        protected readonly ManualResetEvent ManualResetEvent;
        private string _fileName;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SyncEntityBase()
        {
            ManualResetEvent = new ManualResetEvent(false);
        }

        #endregion


        #region Public Properies

        /// <summary>
        /// Name of sync file
        /// </summary>
        public string FileName
        {
            get { return _fileName; }
            set { SetProperty(ref _fileName, value, () => FileName); }
        }

        /// <summary>
        /// SyncMode indicate sync direction between server and client.
        /// </summary>
        public SyncMode Mode
        {
            get { return _mode; }
            set { SetProperty(ref _mode, value, () => Mode); }
        }

        /// <summary>
        /// SyncStatus indicate sync status.
        /// </summary>
        public SyncStatus Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value, () => Status); }
        }

        /// <summary>
        /// Total of file size.
        /// </summary>
        public long FileSize
        {
            get { return _fileSize; }
            set { SetProperty(ref _fileSize, value, () => FileSize); }
        }

        /// <summary>
        /// Progress indicate sync progress
        /// </summary>
        public long Progress
        {
            get { return _progress; }
        }

        /// <summary>
        /// ErrorMessage for sync
        /// </summary>
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value, () => ErrorMessage); }
        }

        #endregion


        #region Methods

        /// <summary>
        /// Pause current sync item
        /// </summary>
        public virtual void Pause()
        {
            Status = SyncStatus.Pause;
        }

        /// <summary>
        /// Stop for stop current sync item
        /// </summary>
        public virtual void Stop()
        {
            Status = SyncStatus.Stop;
        }

        /// <summary>
        /// Start current sync item
        /// </summary>
        public virtual void Start()
        {
            if (Status == SyncStatus.Sync)
                return;

            ManualResetEvent.Set();
            Status = SyncStatus.Sync;
        }

        #endregion


        #region Methods

        /// <summary>
        /// Set Progress by offset value.
        /// </summary>
        /// <param name="offset">Offset of file byte.</param>
        protected void SetProgress(long offset)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _progress = Convert.ToInt32(offset / (double)FileSize * 100);
                RaisePropertyChanged(() => Progress);
            }));
        }

        #endregion

        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SyncEntityBase> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.FileName)
                .ContainsProperty(p => p.Progress)
                .ContainsProperty(p => p.Mode)
                .ContainsProperty(p => p.Status)
                .ContainsProperty(p => p.ErrorMessage);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.FileName)
                    .ContainsProperty(p => p.Progress)
                    .ContainsProperty(p => p.Mode)
                    .ContainsProperty(p => p.Status)
                .EndGroup()
                .Group("Error")
                    .ContainsProperty(p => p.ErrorMessage);

            builder.Property(p => p.FileName).ReadOnly().EndProperty()
                .Property(p => p.Mode).ReadOnly().EndProperty()
                .Property(p => p.Status).ReadOnly().EndProperty()
                .Property(p => p.ErrorMessage).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SyncEntityBase> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.FileName);

            builder.DataFormEditors()
                .Property(p => p.Progress).Hidden();

            builder.Property(p => p.Progress)
                .ReplaceEditor(new ProgressEditorDefinition { Minimum = 0, Maximum = 100 });

            builder.DataFormEditors().Property(p => p.Mode)
                .ReplaceEditor(new OptionEditorDefinition(typeof(SyncMode)));

            builder.GridBaseColumnEditors().Property(p => p.Mode)
                .ReplaceEditor(new PopupOptionEditorDefinition(typeof(SyncMode)));

            builder.DataFormEditors().Property(p => p.Status)
                .ReplaceEditor(new OptionEditorDefinition(typeof(SyncStatus)));

            builder.GridBaseColumnEditors().Property(p => p.Status)
                .ReplaceEditor(new PopupOptionEditorDefinition(typeof(SyncStatus)));

            builder.Property(p => p.ErrorMessage)
                .ReplaceEditor(new CommentEditorDefinition())
                .HideLabel();
        }

        #endregion
    }
}