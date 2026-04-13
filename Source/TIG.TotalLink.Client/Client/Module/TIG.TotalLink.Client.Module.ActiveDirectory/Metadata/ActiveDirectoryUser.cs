using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.ActiveDirectory;

namespace TIG.TotalLink.Shared.DataModel.ActiveDirectory
{
    [FacadeType(typeof(IActiveDirectoryFacade))]
    [DisplayField("DisplayName")]
    public partial class ActiveDirectoryUser
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<ActiveDirectoryUser> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.IsActive)
                .ContainsProperty(p => p.DisplayName)
                .ContainsProperty(p => p.LoginName)
                .ContainsProperty(p => p.Title)
                .ContainsProperty(p => p.FirstName)
                .ContainsProperty(p => p.LastName)
                .ContainsProperty(p => p.Company)
                .ContainsProperty(p => p.Department);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.IsActive)
                    .ContainsProperty(p => p.DisplayName)
                    .ContainsProperty(p => p.LoginName)
                    .ContainsProperty(p => p.Title)
                    .ContainsProperty(p => p.FirstName)
                    .ContainsProperty(p => p.LastName)
                .EndGroup()
                .Group("Organisation")
                    .ContainsProperty(p => p.Company)
                    .ContainsProperty(p => p.Department)
                    .ContainsProperty(p => p.Manager)
                    .ContainsProperty(p => p.Office)
                .EndGroup()
                .Group("Communication")
                    .ContainsProperty(p => p.HomePhone)
                    .ContainsProperty(p => p.Mobile)
                    .ContainsProperty(p => p.Fax)
                    .ContainsProperty(p => p.Pager)
                    .ContainsProperty(p => p.IpPhone)
                .EndGroup()
                .Group("Additional")
                    .ContainsProperty(p => p.DistinguishedName)
                    .ContainsProperty(p => p.DomainName)
                    .ContainsProperty(p => p.SamAccountName)
                    .ContainsProperty(p => p.Sid)
                    .ContainsProperty(p => p.UserAccountControl)
                    .ContainsProperty(p => p.WhenCreated)
                    .ContainsProperty(p => p.WhenChanged);

            builder.Property(p => p.IsActive).ReadOnly();
            builder.Property(p => p.DisplayName).ReadOnly();
            builder.Property(p => p.Title).ReadOnly();
            builder.Property(p => p.FirstName).ReadOnly();
            builder.Property(p => p.LastName).ReadOnly();
            builder.Property(p => p.Company).ReadOnly();
            builder.Property(p => p.Department).ReadOnly();
            builder.Property(p => p.Manager).ReadOnly();
            builder.Property(p => p.Office).ReadOnly();
            builder.Property(p => p.HomePhone).ReadOnly();
            builder.Property(p => p.Fax).ReadOnly();
            builder.Property(p => p.Pager).ReadOnly();
            builder.Property(p => p.IpPhone).ReadOnly();
            builder.Property(p => p.UserAccountControl).ReadOnly();
            builder.Property(p => p.DistinguishedName).ReadOnly();
            builder.Property(p => p.Sid).ReadOnly();
            builder.Property(p => p.WhenCreated).ReadOnly();
            builder.Property(p => p.WhenChanged).ReadOnly();
            builder.Property(p => p.SamAccountName).DisplayName("SAM Account Name").ReadOnly();
            builder.Property(p => p.DomainName).ReadOnly();
            builder.Property(p => p.LoginName).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<ActiveDirectoryUser> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.DisplayName);

            builder.GridBaseColumnEditors()
                .Property(p => p.Manager).Hidden().EndProperty()
                .Property(p => p.Office).Hidden().EndProperty()
                .Property(p => p.HomePhone).Hidden().EndProperty()
                .Property(p => p.Pager).Hidden().EndProperty()
                .Property(p => p.Mobile).Hidden().EndProperty()
                .Property(p => p.Fax).Hidden().EndProperty()
                .Property(p => p.IpPhone).Hidden().EndProperty()
                .Property(p => p.WhenCreated).Hidden().EndProperty()
                .Property(p => p.WhenChanged).Hidden().EndProperty()
                .Property(p => p.UserAccountControl).Hidden().EndProperty()
                .Property(p => p.DistinguishedName).Hidden().EndProperty()
                .Property(p => p.Sid).Hidden().EndProperty()
                .Property(p => p.SamAccountName).Hidden().EndProperty()
                .Property(p => p.DomainName).Hidden().EndProperty();

            builder.Property(p => p.DisplayName).ColumnWidth(300);
            builder.Property(p => p.LoginName).ColumnWidth(300);

            // TODO : UserAccountControl property - Create new Flags editor
            // TODO : Sid property - Allow text editor to display byte[] as string
        }

        #endregion
    }
}