using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Input;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.BackgroundService;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Repository.DataModel;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Repository;
using File = TIG.TotalLink.Shared.DataModel.Repository.File;

namespace TIG.TotalLink.Client.Module.Repository.ViewModel.Widget
{
    public class RepositoryFileListViewModel : ListViewModelBase<File>
    {
        #region Private Fields

        private readonly IRepositoryFacade _repositoryFacade;
        private readonly ISyncBackgroundService _uploadBackgroundService;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public RepositoryFileListViewModel()
        {
            AddFileBinderCommand = new AsyncCommandEx(OnAddFileBinderExecuteAsync, OnAddFileBinderCanExecute);
            AddFileGroupCommand = new AsyncCommandEx(OnAddFileGroupExecuteAsync, OnAddFileGroupCanExecute);
        }

        /// <summary>
        /// Constructor with services.
        /// </summary>
        /// <param name="repositoryFacade">Repository facade for invoke service.</param>
        /// <param name="uploadBackgroundService">upload backgrpund service</param>
        public RepositoryFileListViewModel(IRepositoryFacade repositoryFacade, ISyncBackgroundService uploadBackgroundService)
            : this()
        {
            _repositoryFacade = repositoryFacade;
            _uploadBackgroundService = uploadBackgroundService;
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        [WidgetCommand("Add File Binder", "File", RibbonItemType.ButtonItem, "Add a new File binder.")]
        public virtual ICommand AddFileBinderCommand { get; private set; }

        /// <summary>
        /// Command to delete all selected items from the list.
        /// </summary>
        [WidgetCommand("Add File Group", "File", RibbonItemType.ButtonItem, "Add a new file group.")]
        public virtual ICommand AddFileGroupCommand { get; private set; }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_repositoryFacade, ServiceTypes.All);

                // Initialize the data source
                ItemsSource = _repositoryFacade.CreateInstantFeedbackSource<File>();
            });
        }

        protected override async Task OnAddExecuteAsync()
        {
            var dialogResult = false;
            string filePath = null;
            File file = null;

            // Save file information to remote database.
            await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);
                file = new File(uow);

                dialogResult = DetailDialogService.ShowDialog(DetailEditMode.Add, file);

                if (!dialogResult)
                {
                    return false;
                }

                // TODO: Currently upload only can have one file select to upload.
                filePath = file.UploadFileList.First();

                // Get file extension from file path.
                var extension = filePath.Split('.').Last();

                var fileExtension = new XPQuery<FileExtension>(uow).FirstOrDefault(p => p.Name == extension);
                // Create new file extension.
                if (fileExtension == null)
                {
                    fileExtension = new FileExtension(uow)
                    {
                        Name = extension
                    };
                }

                file.Extension = fileExtension;

                return true;
            });

            if (dialogResult == false
                || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            // Read file by file path.
            var fileInfo = new FileInfo(filePath);
            var fileLenght = fileInfo.Length;
            var fileData = new byte[fileLenght];
            using (var fs = System.IO.File.OpenRead(filePath))
            {
                fs.Read(fileData, 0, (int)fileLenght);
            }

            // Get file hash
            byte[] hash;
            using (var hasher = MD5.Create())
            {
                hash = hasher.ComputeHash(fileData);
            }

            // Save file to local cache database.
            var fileDataId = Guid.NewGuid();
            await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                new FileData(uow)
                {
                    Oid = fileDataId,
                    Body = fileData,
                    Size = fileData.Length,
                    CreatedOn = DateTime.Now,
                    Version = 0,
                    HashHex = hash
                };
            }, ServiceTypes.LocalData);

            // Add file to background queue.
            _uploadBackgroundService.Enqueue(file.Oid, fileDataId, SyncMode.Upload);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        private async Task OnAddFileBinderExecuteAsync()
        {
            await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);

                // If UseAddDialog = true, show a dialog to configure the new item
                if (UseAddDialog)
                {
                    return DetailDialogService.ShowDialog(DetailEditMode.Add, new FileBinder(uow));
                }

                // If UseAddDialog = false, save the item immediately
                return true;
            });
        }

        /// <summary>
        /// CanExecute method for the AddCommand.
        /// </summary>
        private bool OnAddFileBinderCanExecute()
        {
            return SelectedItems.Count > 0 && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        private async Task OnAddFileGroupExecuteAsync()
        {
            await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);

                // If UseAddDialog = true, show a dialog to configure the new item
                if (UseAddDialog)
                {
                    return DetailDialogService.ShowDialog(DetailEditMode.Add, new FileGroup(uow));
                }

                // If UseAddDialog = false, save the item immediately
                return true;
            });
        }

        /// <summary>
        /// CanExecute method for the AddCommand.
        /// </summary>
        private bool OnAddFileGroupCanExecute()
        {
            return SelectedItems.Count > 0 && CanExecuteWidgetCommand;
        }

        #endregion
    }
}