using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    [DisplayField("Name")]
    [EntityFilter(typeof(State), "State.Oid IN (?)", "State IN (?)")]
    [EntityFilter(typeof(Country), "State.Country.Oid IN (?)", "Country IN (?)")]
    public partial class Postcode
    {
        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<Postcode> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Code)
                .ContainsProperty(p => p.State)
                .ContainsProperty(p => p.Latitude)
                .ContainsProperty(p => p.Longitude)
                .ContainsProperty(p => p.Region)
                .ContainsProperty(p => p.Area)
                .ContainsProperty(p => p.Timezone)
                .ContainsProperty(p => p.Utc)
                .ContainsProperty(p => p.External);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name)
                    .ContainsProperty(p => p.Code)
                    .ContainsProperty(p => p.State)
                    .ContainsProperty(p => p.Latitude)
                    .ContainsProperty(p => p.Longitude)
                    .ContainsProperty(p => p.Region)
                    .ContainsProperty(p => p.Area)
                    .ContainsProperty(p => p.Timezone)
                    .ContainsProperty(p => p.Utc)
                    .ContainsProperty(p => p.External);

            builder.Property(p => p.Name).Required();
            builder.Property(p => p.State).Required();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<Postcode> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);
        }

        #endregion
    }
}
