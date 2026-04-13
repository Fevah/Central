using DevExpress.Data.Filtering;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum.CRM;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Shared.DataModel.Crm
{
    [FacadeType(typeof(ICrmFacade))]
    [DisplayField("FirstName")]
    public partial class Contact
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Contact> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Reference)
                .ContainsProperty(p => p.LegacyReference)
                .ContainsProperty(p => p.ContactType)
                .ContainsProperty(p => p.OwnershipType)
                .ContainsProperty(p => p.IsVendor)
                .ContainsProperty(p => p.FirstName)
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
                    .ContainsProperty(p => p.LegacySource)
                .EndGroup()
                .Group("Communication")
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
                .Group("Invoicing")
                    .ContainsProperty(p => p.ShipmentMethod)
                    .ContainsProperty(p => p.AllowLineDiscount)
                    .ContainsProperty(p => p.GSTIncluded)
                    .ContainsProperty(p => p.PrepaymentPercent)
                    .ContainsProperty(p => p.BackOrderNotAccepted)
                    .ContainsProperty(p => p.GSTBusinessPostingGroup)
                    .ContainsProperty(p => p.GeneralBusinessPostingGroup)
                    .ContainsProperty(p => p.ContactPostingGroup)
                    .ContainsProperty(p => p.AllowPartialDelivery)
                .EndGroup()
                .Group("Payments")
                    .ContainsProperty(p => p.PaymentTerms)
                    .ContainsProperty(p => p.PaymentMethod)
                    .ContainsProperty(p => p.ReminderTerms)
                    .ContainsProperty(p => p.CashFlowPaymentTerms)
                .EndGroup()
                .Group("Foreign Trade")
                    .ContainsProperty(p => p.Currency)
                    .ContainsProperty(p => p.Language)
                .EndGroup()
                .Group("Registration")
                    .ContainsProperty(p => p.ACN)
                    .ContainsProperty(p => p.ABN)
                    .ContainsProperty(p => p.CompanyRegistered)
                .EndGroup()
                .Group("Groups")
                    .ContainsProperty(p => p.ContactGroupLinks)
                .EndGroup()
                .Group("Relationships")
                    .ContainsProperty(p => p.SourceContactLinks)
                    .ContainsProperty(p => p.TargetContactLinks)
                .EndGroup()
                .Group("Addresses")
                    .ContainsProperty(p => p.Addresses);

            builder.Property(p => p.Reference).ReadOnly();
            builder.Property(p => p.FirstName)
                .DisplayName("Name")
                .Required();
            builder.Property(p => p.Balance).ReadOnly();
            builder.Property(p => p.PrepaymentPercent).DisplayName("Prepayment %");
            builder.Property(p => p.ContactGroupLinks)
                .AutoGenerated()
                .DisplayName("Groups");
            builder.Property(p => p.SourceContactLinks)
                .AutoGenerated()
                .DisplayName("Parents");
            builder.Property(p => p.TargetContactLinks)
                .AutoGenerated()
                .DisplayName("Children");
            builder.Property(p => p.Addresses).AutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Contact> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.FirstName);

            builder.GridColumnEditors().Group()
                .ContainsProperty(p => p.ContactType);

            builder.GridBaseColumnEditors()
                .Property(p => p.IndustryClass).Hidden().EndProperty()
                .Property(p => p.Fax).Hidden().EndProperty()
                .Property(p => p.BusinessPhone).Hidden().EndProperty()
                .Property(p => p.BusinessPhone2).Hidden().EndProperty()
                .Property(p => p.BusinessFax).Hidden().EndProperty()
                .Property(p => p.BusinessMobile).Hidden().EndProperty()
                .Property(p => p.BusinessExtension).Hidden().EndProperty()
                .Property(p => p.Email).Hidden().EndProperty()
                .Property(p => p.Webpage).Hidden().EndProperty()
                .Property(p => p.Facebook).Hidden().EndProperty()
                .Property(p => p.Twitter).Hidden().EndProperty()
                .Property(p => p.InternetChat).Hidden().EndProperty()
                .Property(p => p.InternetChatType).Hidden().EndProperty()
                .Property(p => p.Balance).Hidden().EndProperty()
                .Property(p => p.CreditLimit).Hidden().EndProperty()
                .Property(p => p.Currency).Hidden().EndProperty()
                .Property(p => p.Language).Hidden().EndProperty()
                .Property(p => p.Blocked).Hidden().EndProperty()
                .Property(p => p.BlockedReason).Hidden().EndProperty()
                .Property(p => p.AllowLineDiscount).Hidden().EndProperty()
                .Property(p => p.GSTIncluded).Hidden().EndProperty()
                .Property(p => p.PrepaymentPercent).Hidden().EndProperty()
                .Property(p => p.BackOrderNotAccepted).Hidden().EndProperty()
                .Property(p => p.PaymentTerms).Hidden().EndProperty()
                .Property(p => p.PaymentMethod).Hidden().EndProperty()
                .Property(p => p.ReminderTerms).Hidden().EndProperty()
                .Property(p => p.CashFlowPaymentTerms).Hidden().EndProperty()
                .Property(p => p.ACN).Hidden().EndProperty()
                .Property(p => p.ABN).Hidden().EndProperty()
                .Property(p => p.CompanyRegistered).Hidden().EndProperty()
                .Property(p => p.BusinessType).Hidden().EndProperty()
                .Property(p => p.GSTBusinessPostingGroup).Hidden().EndProperty()
                .Property(p => p.GeneralBusinessPostingGroup).Hidden().EndProperty()
                .Property(p => p.ContactPostingGroup).Hidden().EndProperty()
                .Property(p => p.ShipmentMethod).Hidden().EndProperty()
                .Property(p => p.LegacySource).Hidden().EndProperty()
                .Property(p => p.ExternalReference).Hidden().EndProperty()
                .Property(p => p.AllowPartialDelivery).Hidden().EndProperty();

            builder.Property(p => p.Reference).GetEditor<SpinEditorDefinition>().DisplayFormat = "D0";

            builder.Property(p => p.ContactGroupLinks).HideLabel();
            builder.Property(p => p.SourceContactLinks).LabelPosition(LayoutItemLabelPosition.Top);
            builder.Property(p => p.TargetContactLinks).LabelPosition(LayoutItemLabelPosition.Top);
            builder.Property(p => p.Addresses).HideLabel();

            builder.Property(p => p.Email).ReplaceEditor(new HyperLinkEditorDefinition() { AllowUrls = false });
            builder.Property(p => p.Webpage).ReplaceEditor(new HyperLinkEditorDefinition() { AllowEmails = false });
            builder.Property(p => p.Facebook).ReplaceEditor(new HyperLinkEditorDefinition() { AllowEmails = false });
            builder.Property(p => p.Twitter).ReplaceEditor(new HyperLinkEditorDefinition() { AllowEmails = false });
            builder.Property(p => p.Balance).ReplaceEditor(new CurrencyEditorDefinition());
            builder.Property(p => p.CreditLimit).ReplaceEditor(new CurrencyEditorDefinition());
            builder.Property(p => p.Blocked)
                .ReplaceEditor(new ComboEditorDefinition(typeof(BlockType)))
                .AllowNull();
            builder.Property(p => p.BlockedReason)
                .UnlimitedLength()
                .ReplaceEditor(new RichTextEditorDefinition());

            builder.GridBaseColumnEditors().Property(p => p.ContactGroupLinks).ReplaceEditor(new PopupGridEditorDefinition()
            {
                EntityType = typeof(ContactGroupLink),
                FilterMethod = context => CriteriaOperator.Parse("Contact.Oid = ?", ((Contact)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.ContactGroupLinks).ReplaceEditor(new GridEditorDefinition
            {
                EntityType = typeof(ContactGroupLink),
                FilterMethod = context => CriteriaOperator.Parse("Contact.Oid = ?", ((Contact)context).Oid)
            });

            builder.GridBaseColumnEditors().Property(p => p.SourceContactLinks).ReplaceEditor(new PopupGridEditorDefinition()
            {
                EntityType = typeof(ContactLink),
                FilterMethod = context => CriteriaOperator.Parse("Source.Oid = ?", ((Contact)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.SourceContactLinks).ReplaceEditor(new GridEditorDefinition()
            {
                EntityType = typeof(ContactLink),
                FilterMethod = context => CriteriaOperator.Parse("Source.Oid = ?", ((Contact)context).Oid)
            });

            builder.GridBaseColumnEditors().Property(p => p.TargetContactLinks).ReplaceEditor(new PopupGridEditorDefinition()
            {
                EntityType = typeof(ContactLink),
                FilterMethod = context => CriteriaOperator.Parse("Target.Oid = ?", ((Contact)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.TargetContactLinks).ReplaceEditor(new GridEditorDefinition()
            {
                EntityType = typeof(ContactLink),
                FilterMethod = context => CriteriaOperator.Parse("Target.Oid = ?", ((Contact)context).Oid)
            });

            builder.GridBaseColumnEditors().Property(p => p.Addresses).ReplaceEditor(new PopupGridEditorDefinition()
            {
                EntityType = typeof(Address),
                FilterMethod = context => CriteriaOperator.Parse("Contact.Oid = ?", ((Contact)context).Oid)
            });

            builder.DataFormEditors().Property(p => p.Addresses).ReplaceEditor(new GridEditorDefinition()
            {
                EntityType = typeof(Address),
                FilterMethod = context => CriteriaOperator.Parse("Contact.Oid = ?", ((Contact)context).Oid)
            });
        }

        #endregion
    }
}
