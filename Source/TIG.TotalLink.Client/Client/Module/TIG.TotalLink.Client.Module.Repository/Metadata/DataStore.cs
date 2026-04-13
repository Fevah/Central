using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Repository;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Shared.DataModel.Repository
{
    [FacadeType(typeof(IRepositoryFacade))]
    [DisplayField("DatabaseName")]
    public partial class DataStore
    {
        #region Properties

        public double TotalSize { get; set; }

        public int ContainerCount { get; set; }

        public int FolderCount { get; set; }

        public int FileCount { get; set; }

        public double FileUsedPercentage { get; set; }

        public string Location { get; set; }

        public DBStorageStatus Status { get; set; }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DataStore> builder)
        {
            builder.TableLayout().Group("")
                    .ContainsProperty(p => p.DatabaseProvider)
                    .ContainsProperty(p => p.Server)
                    .ContainsProperty(p => p.DatabaseName)
                    .ContainsProperty(p => p.IntegratedSecurity)
                    .ContainsProperty(p => p.UserName)
                    .ContainsProperty(p => p.Password)
                    .ContainsProperty(p => p.Type)
                    .ContainsProperty(p => p.Status)
                    .ContainsProperty(p => p.DataFileSizeLimit)
                    .ContainsProperty(p => p.FileUsedPercentage)
                    .ContainsProperty(p => p.TotalSize)
                    .ContainsProperty(p => p.FileExtensionGroup)
                    .ContainsProperty(p => p.ContainerCount)
                    .ContainsProperty(p => p.FolderCount)
                    .ContainsProperty(p => p.FileCount)
                    .ContainsProperty(p => p.Location);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.DatabaseProvider)
                    .ContainsProperty(p => p.Server)
                    .ContainsProperty(p => p.DatabaseName)
                    .ContainsProperty(p => p.IntegratedSecurity)
                    .ContainsProperty(p => p.UserName)
                    .ContainsProperty(p => p.Password)
                    .ContainsProperty(p => p.IsDefault)
                    .ContainsProperty(p => p.Type)
                    .ContainsProperty(p => p.FileUsedPercentage)
                    .ContainsProperty(p => p.DataFileSizeLimit)
                    .ContainsProperty(p => p.FileExtensionGroup);

            builder.Property(p => p.DatabaseProvider).Required();
            builder.Property(p => p.Server).Required();
            builder.Property(p => p.DatabaseName).Required();
            builder.Property(p => p.FileExtensionGroup).Required();
            builder.Property(p => p.Type).Required();
            builder.Property(p => p.DataFileSizeLimit)
                .Required()
                .DisplayName("Max File Size (MB)");
            builder.Property(p => p.FileUsedPercentage)
                .DisplayName("File Used")
                .ReadOnly();
            builder.Property(p => p.ContainerCount)
                .DisplayName("Containers")
                .ReadOnly();
            builder.Property(p => p.FolderCount)
                .DisplayName("Folders")
                .ReadOnly();
            builder.Property(p => p.FileCount)
                .DisplayName("Files")
                .ReadOnly();
            builder.Property(p => p.Status).ReadOnly();
            builder.Property(p => p.Location).ReadOnly();
            builder.Property(p => p.TotalSize)
                .DisplayName("Database Files Size (MB)")
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DataStore> builder)
        {
            builder.DataFormEditors()
                .Property(p => p.Status).Hidden().EndProperty()
                .Property(p => p.ContainerCount).Hidden().EndProperty()
                .Property(p => p.FileCount).Hidden().EndProperty()
                .Property(p => p.FileUsedPercentage).Hidden().EndProperty()
                .Property(p => p.FileVersions).Hidden().EndProperty()
                .Property(p => p.FolderCount).Hidden().EndProperty()
                .Property(p => p.Location).Hidden().EndProperty()
                .Property(p => p.TotalSize).Hidden();

            builder.GridBaseColumnEditors()
                .Property(p => p.DataFileSizeLimit).ReadOnly().EndProperty()
                .Property(p => p.Type).ReadOnly().EndProperty()
                .Property(p => p.DatabaseProvider).ReadOnly().EndProperty()
                .Property(p => p.DatabaseName).ReadOnly();

            builder.GridBaseColumnEditors().Property(p => p.FileVersions).Hidden();

            builder.Property(p => p.Password).ReplaceEditor(new PasswordEditorDefinition());
            builder.Property(p => p.FileUsedPercentage).ReplaceEditor(new ProgressEditorDefinition { Minimum = 0, Maximum = 1 });

            builder.GridBaseColumnEditors().Property(p => p.Status)
                .ReplaceEditor(new PopupOptionEditorDefinition(typeof(DBStorageStatus)));

            builder.DataFormEditors()
                .Property(p => p.DatabaseProvider)
                .ReplaceEditor(new ComboEditorDefinition(typeof(DatabaseProvider)));
        }

        #endregion
    }
}