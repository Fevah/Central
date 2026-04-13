using System.ComponentModel.DataAnnotations;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client.Module.Admin.Uploader.Core
{
    /// <summary>
    /// Base class for data models that will be used by an uploader widget.
    /// </summary>
    public abstract class UploaderDataModelBase : LocalDataObjectBase
    {
        #region Private Fields

        private UploaderResult _uploadResult;
        private System.Exception _uploadException;
        private string _uploadErrorMessage;

        #endregion


        #region Public Properties

        /// <summary>
        /// Describes the result of the upload process.
        /// </summary>
        public UploaderResult UploadResult
        {
            get { return _uploadResult; }
            set { SetProperty(ref _uploadResult, value, () => UploadResult); }
        }

        /// <summary>
        /// Contains the exception that was captured if the upload failed.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public System.Exception UploadException
        {
            get { return _uploadException; }
            set
            {
                SetProperty(ref _uploadException, value, () => UploadException, () =>
                    {
                        // Parse the exception to populate UploadErrorMessage
                        var serviceException = new ServiceExceptionHelper(UploadException);
                        UploadErrorMessage = serviceException.Message;
                    }
                );
            }
        }

        /// <summary>
        /// Contains the error message from the exception that was captured if the upload failed.
        /// </summary>
        public string UploadErrorMessage
        {
            get { return _uploadErrorMessage; }
            set { SetProperty(ref _uploadErrorMessage, value, () => UploadErrorMessage); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<UploaderDataModelBase> builder)
        {
            builder.Group("")
                .ContainsProperty(p => p.UploadResult)
                .ContainsProperty(p => p.UploadErrorMessage);

            builder.Property(p => p.UploadResult).ReadOnly();
            builder.Property(p => p.UploadErrorMessage)
                .DisplayName("Upload Error")
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<UploaderDataModelBase> builder)
        {
            builder.Property(p => p.UploadResult).ReplaceEditor(new ComboEditorDefinition(typeof(UploaderResult)));
            builder.Property(p => p.UploadErrorMessage).ReplaceEditor(new MemoEditorDefinition());
        }

        #endregion

    }
}
