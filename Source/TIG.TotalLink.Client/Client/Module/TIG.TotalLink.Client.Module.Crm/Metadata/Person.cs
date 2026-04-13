using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("FullName")]
    [EntityFilter(typeof(Branch), "SourceContactLinks[Target.Oid IN (?)]", "Parents IN (?)")]
    public partial class Person
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Person> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Reference)
                .ContainsProperty(p => p.LegacyReference)
                .ContainsProperty(p => p.IsVendor)
                .ContainsProperty(p => p.FullName)
                .ContainsProperty(p => p.SearchName)
                .ContainsProperty(p => p.ContactGroupLinks)
                .ContainsProperty(p => p.SourceContactLinks)
                .ContainsProperty(p => p.TargetContactLinks)
                .ContainsProperty(p => p.Addresses);

            builder.DataFormLayout()
                .ContainsProperty(p => p.ContactType)
                .TabbedGroup("Tabs").Group("General")
                    .ContainsProperty(p => p.Reference)
                    .ContainsProperty(p => p.LegacyReference)
                    .ContainsProperty(p => p.ExternalReference)
                    .ContainsProperty(p => p.IsVendor)
                    .ContainsProperty(p => p.FullName)
                    .ContainsProperty(p => p.Title)
                    .ContainsProperty(p => p.StaffRole)
                    .ContainsProperty(p => p.FirstName)
                    .ContainsProperty(p => p.MiddleName)
                    .ContainsProperty(p => p.LastName)
                    .ContainsProperty(p => p.Nickname)
                    .ContainsProperty(p => p.SearchName)
                    .ContainsProperty(p => p.Gender)
                    .ContainsProperty(p => p.Birthdate)
                    .ContainsProperty(p => p.Balance)
                    .ContainsProperty(p => p.CreditLimit)
                    .ContainsProperty(p => p.Blocked)
                    .ContainsProperty(p => p.BlockedReason)
                .EndGroup()
                .Group("Communication")
                    .ContainsProperty(p => p.HomePhone)
                    .ContainsProperty(p => p.HomePhone2)
                    .ContainsProperty(p => p.Mobile)
                    .ContainsProperty(p => p.Pager)
                    .ContainsProperty(p => p.Fax)
                    .ContainsProperty(p => p.BusinessPhone)
                    .ContainsProperty(p => p.BusinessPhone2)
                    .ContainsProperty(p => p.BusinessFax)
                    .ContainsProperty(p => p.BusinessMobile)
                    .ContainsProperty(p => p.BusinessExtension)
                    .ContainsProperty(p => p.Email)
                    .ContainsProperty(p => p.Webpage)
                    .ContainsProperty(p => p.Facebook)
                    .ContainsProperty(p => p.Twitter)
                    .ContainsProperty(p => p.InternetChat)
                    .ContainsProperty(p => p.InternetChatType)
                .EndGroup()
                .Group("Subsidy")
                    .ContainsProperty(p => p.SubsidyName)
                    .ContainsProperty(p => p.SubsidyValue);

            builder.Property(p => p.Reference).ReadOnly();
            builder.Property(p => p.FirstName).DisplayName("First Name");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Person> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.FullName);

            builder.Group();

            builder.GridBaseColumnEditors()
                .Property(p => p.ContactType).Hidden().EndProperty()
                .Property(p => p.OwnershipType).Hidden().EndProperty()
                .Property(p => p.FirstName).Hidden().EndProperty()
                .Property(p => p.MiddleName).Hidden().EndProperty()
                .Property(p => p.LastName).Hidden().EndProperty()
                .Property(p => p.Gender).Hidden().EndProperty()
                .Property(p => p.Title).Hidden().EndProperty()
                .Property(p => p.Birthdate).Hidden().EndProperty()
                .Property(p => p.Nickname).Hidden().EndProperty()
                .Property(p => p.HomePhone).Hidden().EndProperty()
                .Property(p => p.HomePhone2).Hidden().EndProperty()
                .Property(p => p.Mobile).Hidden().EndProperty()
                .Property(p => p.Pager).Hidden().EndProperty()
                .Property(p => p.ExternalReference).Hidden().EndProperty()
                .Property(p => p.SubsidyName).Hidden().EndProperty()
                .Property(p => p.SubsidyValue).Hidden().EndProperty()
                .Property(p => p.StaffRole).Hidden().EndProperty();

            builder.Property(p => p.SubsidyValue)
                .ReplaceEditor(new CurrencyEditorDefinition());

            builder.Property(p => p.Gender)
                .ReplaceEditor(new ComboEditorDefinition(typeof(Gender)))
                .AllowNull();
        }

        /// <summary>
        /// Builds metadata for form editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        public void BuildFormMetadata(EditorMetadataBuilder<Person> builder, DataLayoutControlEx dataLayoutControl)
        {
            if (dataLayoutControl.EditMode == DetailEditMode.Add)
            {
                builder.DataFormEditors()
                    .Property(p => p.FullName).Hidden().EndProperty();
            }
            else
            {
                builder.DataFormEditors()
                    .Property(p => p.FullName).Visible().EndProperty();
            }
        }

        #endregion
    }
}
