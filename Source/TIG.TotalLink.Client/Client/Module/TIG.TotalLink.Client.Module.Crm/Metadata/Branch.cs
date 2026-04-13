using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("FirstName")]
    [EntityFilter(typeof(Company), "SourceContactLinks[Target.Oid IN (?)]", "Parents IN (?)")]
    public partial class Branch
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Branch> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Reference)
                .ContainsProperty(p => p.LegacyReference)
                .ContainsProperty(p => p.ContactType)
                .ContainsProperty(p => p.OwnershipType)
                .ContainsProperty(p => p.BranchType)
                .ContainsProperty(p => p.BranchNumber)
                .ContainsProperty(p => p.IsVendor)
                .ContainsProperty(p => p.FirstName)
                .ContainsProperty(p => p.SearchName)
                .ContainsProperty(p => p.ContactGroupLinks)
                .ContainsProperty(p => p.SourceContactLinks)
                .ContainsProperty(p => p.TargetContactLinks)
                .ContainsProperty(p => p.Addresses);

            builder.DataFormLayout()
                .ContainsProperty(p => p.ContactType)
                .ContainsProperty(p => p.BranchType)
                .TabbedGroup("Tabs").Group("General")
                    .ContainsProperty(p => p.Reference)
                    .ContainsProperty(p => p.LegacyReference)
                    .ContainsProperty(p => p.BranchNumber)
                    .ContainsProperty(p => p.IsVendor)
                    .ContainsProperty(p => p.FirstName)
                    .ContainsProperty(p => p.SearchName)
                    .ContainsProperty(p => p.Balance)
                    .ContainsProperty(p => p.CreditLimit)
                    .ContainsProperty(p => p.Blocked)
                    .ContainsProperty(p => p.BlockedReason)
                .EndGroup()
                .Group("Additional")
                    .ContainsProperty(p => p.BusinessType)
                    .ContainsProperty(p => p.OwnershipType)
                    .ContainsProperty(p => p.IndustryClass)
                    .ContainsProperty(p => p.TotalStores)
                    .ContainsProperty(p => p.TotalEmployees)
                    .ContainsProperty(p => p.GlobalRegion)
                    .ContainsProperty(p => p.LegacySource)
                .EndGroup();

            builder.Property(p => p.Reference).ReadOnly();
            builder.Property(p => p.BranchType).Required();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Branch> builder)
        {
            builder.Group()
                .ContainsProperty(p => p.BranchType);

            builder.GridBaseColumnEditors()
                .Property(p => p.ContactType).Hidden().EndProperty();
        }

        #endregion
    }
}
