using System.Collections.ObjectModel;
using System.IO;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Shared.DataModel.Repository
{
    [FacadeType(typeof(IRepositoryFacade))]
    [DisplayField("Name")]
    partial class File
    {
        #region Private Properties

        private ObservableCollection<string> _uploadFileList;

        #endregion


        #region Public Properties

        [NonPersistent]
        public ObservableCollection<string> UploadFileList
        {
            get { return _uploadFileList ?? (_uploadFileList = new ObservableCollection<string>()); }
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<File> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Extension)
                .ContainsProperty(p => p.Description)
                .ContainsProperty(p => p.Searchable)
                .ContainsProperty(p => p.Notes)
                .ContainsProperty(p => p.Barcode)
                .ContainsProperty(p => p.Comments)
                .ContainsProperty(p => p.ActionableItem)
                .ContainsProperty(p => p.DataReceived)
                .ContainsProperty(p => p.Confidential)
                .ContainsProperty(p => p.IsRecord)
                .ContainsProperty(p => p.RecordMetadata)
                .ContainsProperty(p => p.Category)
            .EndGroup()
            .Group("Manage")
                .ContainsProperty(p => p.CreatedOn)
                .ContainsProperty(p => p.CreateBy)
                .ContainsProperty(p => p.UpdateOn)
                .ContainsProperty(p => p.UpdateBy)
                .ContainsProperty(p => p.ApprovedOn)
                .ContainsProperty(p => p.ApprovedBy)
                .ContainsProperty(p => p.CheckOutOn)
                .ContainsProperty(p => p.CheckOutBy)
                .ContainsProperty(p => p.DeleteOn)
                .ContainsProperty(p => p.LastVersion)
            .EndGroup()
            .Group("Email")
                .ContainsProperty(p => p.EmailFrom)
                .ContainsProperty(p => p.EmailCc)
                .ContainsProperty(p => p.EmailTo)
                .ContainsProperty(p => p.EmailSubject)
                .ContainsProperty(p => p.EmailBody)
                .ContainsProperty(p => p.EmailAttachments)
            .EndGroup()
            .Group("Versions")
                .ContainsProperty(p => p.FileVersions)
            .EndGroup()
            .Group("Binder Versions")
                .ContainsProperty(p => p.FileBinderVersions)
            .EndGroup()
            .Group("Filter Options")
                .ContainsProperty(p => p.FilterOptions);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                        .ContainsProperty(p => p.Name)
                        .ContainsProperty(p => p.Extension)
                        .ContainsProperty(p => p.Description)
                        .ContainsProperty(p => p.Searchable)
                        .ContainsProperty(p => p.Notes)
                        .ContainsProperty(p => p.Barcode)
                        .ContainsProperty(p => p.Comments)
                        .ContainsProperty(p => p.ActionableItem)
                        .ContainsProperty(p => p.DataReceived)
                        .ContainsProperty(p => p.Confidential)
                        .ContainsProperty(p => p.IsRecord)
                        .ContainsProperty(p => p.RecordMetadata)
                        .ContainsProperty(p => p.Category)
                    .EndGroup()
                    .Group("Upload files")
                        .ContainsProperty(p => p.UploadFileList)
                    .EndGroup()
                    .Group("Email")
                        .ContainsProperty(p => p.EmailFrom)
                        .ContainsProperty(p => p.EmailCc)
                        .ContainsProperty(p => p.EmailTo)
                        .ContainsProperty(p => p.EmailSubject)
                        .ContainsProperty(p => p.EmailBody)
                        .ContainsProperty(p => p.EmailAttachments)
                    .EndGroup()
                        .Group("Versions")
                            .ContainsProperty(p => p.LastVersion)
                            .ContainsProperty(p => p.FileVersions)
                    .EndGroup()
                        .Group("Binder Versions")
                            .ContainsProperty(p => p.FileBinderVersions)
                    .EndGroup()
                        .Group("Group Versions")
                            .ContainsProperty(p => p.FileGroupVersions)
                    .EndGroup()
                    .Group("Filter Options")
                        .ContainsProperty(p => p.FilterOptions);

            builder.Property(p => p.ApprovedOn).ReadOnly().EndProperty()
                .Property(p => p.CreatedOn).ReadOnly().EndProperty()
                .Property(p => p.CreateBy).ReadOnly().EndProperty()
                .Property(p => p.UpdateOn).ReadOnly().EndProperty()
                .Property(p => p.UpdateBy).ReadOnly().EndProperty()
                .Property(p => p.ApprovedOn).ReadOnly().EndProperty()
                .Property(p => p.ApprovedBy).ReadOnly().EndProperty()
                .Property(p => p.CheckOutOn).ReadOnly().EndProperty()
                .Property(p => p.CheckOutBy).ReadOnly().EndProperty()
                .Property(p => p.DeleteOn).ReadOnly().EndProperty()
                .Property(p => p.Extension).ReadOnly().EndProperty()
                .Property(p => p.LastVersion).ReadOnly().EndProperty();

            builder.Property(p => p.Name).Required();

            builder.Property(p => p.FileVersions).AutoGenerated();
            builder.Property(p => p.FileBinderVersions).AutoGenerated();
            builder.Property(p => p.FileGroupVersions).AutoGenerated();
            builder.Property(p => p.FilterOptions).AutoGenerated();
            builder.Property(p => p.UploadFileList).AutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<File> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.GridBaseColumnEditors().Property(p => p.ApprovedOn).Hidden().EndProperty()
                .Property(p => p.ApprovedBy).Hidden().EndProperty()
                .Property(p => p.CheckOutOn).Hidden().EndProperty()
                .Property(p => p.CheckOutBy).Hidden().EndProperty()
                .Property(p => p.DeleteOn).Hidden().EndProperty()
                .Property(p => p.CreatedOn).Hidden().EndProperty()
                .Property(p => p.CreateBy).Hidden().EndProperty()
                .Property(p => p.UpdateOn).Hidden().EndProperty()
                .Property(p => p.UpdateBy).Hidden().EndProperty()
                .Property(p => p.LastVersion).Hidden().EndProperty()
                .Property(p => p.EmailFrom).Hidden().EndProperty()
                .Property(p => p.EmailCc).Hidden().EndProperty()
                .Property(p => p.EmailTo).Hidden().EndProperty()
                .Property(p => p.EmailSubject).Hidden().EndProperty()
                .Property(p => p.EmailBody).Hidden().EndProperty()
                .Property(p => p.EmailAttachments).Hidden().EndProperty()
                .Property(p => p.UploadFileList).Hidden().EndProperty();

            builder.DataFormEditors().Property(p => p.ApprovedOn).Hidden().EndProperty()
                .Property(p => p.ApprovedBy).Hidden().EndProperty()
                .Property(p => p.CheckOutOn).Hidden().EndProperty()
                .Property(p => p.CheckOutBy).Hidden().EndProperty()
                .Property(p => p.DeleteOn).Hidden().EndProperty()
                .Property(p => p.CreatedOn).Hidden().EndProperty()
                .Property(p => p.CreateBy).Hidden().EndProperty()
                .Property(p => p.UpdateOn).Hidden().EndProperty()
                .Property(p => p.UpdateBy).Hidden().EndProperty()
                .Property(p => p.LastVersion).Hidden().EndProperty();

            builder.DataFormEditors().Property(p => p.FileVersions).ReplaceEditor(new GridEditorDefinition
            {
                EntityType = typeof(FileVersion),
                FilterMethod = context => CriteriaOperator.Parse("File.Oid = ?", ((File)context).Oid)
            }).HideLabel();

            builder.GridBaseColumnEditors().Property(p => p.FileVersions).ReplaceEditor(new PopupGridEditorDefinition
            {
                EntityType = typeof(FileVersion),
                FilterMethod = context => CriteriaOperator.Parse("File.Oid = ?", ((File)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.FileBinderVersions).ReplaceEditor(new GridEditorDefinition
            {
                EntityType = typeof(FileBinderVersion),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            }).HideLabel();

            builder.GridBaseColumnEditors().Property(p => p.FileBinderVersions).ReplaceEditor(new PopupGridEditorDefinition
            {
                EntityType = typeof(FileBinderVersion),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.FileGroupVersions).ReplaceEditor(new GridEditorDefinition
            {
                EntityType = typeof(FileGroupVersion),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            }).HideLabel();

            builder.GridBaseColumnEditors().Property(p => p.FileGroupVersions).ReplaceEditor(new PopupGridEditorDefinition
            {
                EntityType = typeof(FileGroupVersion),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.FilterOptions).ReplaceEditor(new GridEditorDefinition
            {
                EntityType = typeof(FilterOption),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            }).HideLabel();

            builder.GridBaseColumnEditors().Property(p => p.FilterOptions).ReplaceEditor(new PopupGridEditorDefinition
            {
                EntityType = typeof(FilterOption),
                FilterMethod = context => CriteriaOperator.Parse("[Files][Oid = ?]", ((File)context).Oid)
            }).HideLabel();

            builder.Property(p => p.UploadFileList).HideLabel().ReplaceEditor(new UploadEditorDefinition
            {
                FileFilter = "All Images | *.bmp;*.jpg;*.jpeg;*.gif;*.png;*.tif |" +
                             "All Document | *.doc;*.docx;*xls;*xlsx |" +
                             "All | *.*"
            });
        }


        /// <summary>
        /// Builds metadata for form editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        public void BuildFormMetadata(EditorMetadataBuilder<File> builder, DataLayoutControlEx dataLayoutControl)
        {
            if (dataLayoutControl.EditMode == DetailEditMode.Edit)
            {
                builder.DataFormEditors()
                    .Property(p => p.UploadFileList).Hidden().EndProperty();
            }
            else
            {
                builder.DataFormEditors()
                    .Property(p => p.UploadFileList).Visible().EndProperty();
            }
        }


        #endregion
    }
}