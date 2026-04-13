using System.Runtime.Serialization;
using DevExpress.Mvvm.DataAnnotations;
using Microsoft.Web.Administration;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;

namespace TIG.TotalLink.Client.IisAdmin
{
    [DataContract]
    public class IisSite
    {
        #region Public Properties

        /// <summary>
        /// The id of the site.
        /// </summary>
        [DataMember]
        public long Id { get; set; }

        /// <summary>
        /// The name of the site.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// The port the site is listening on.
        /// </summary>
        [DataMember]
        public int? Port { get; set; }

        /// <summary>
        /// The state of the site.
        /// </summary>
        [DataMember]
        public ObjectState State { get; set; }

        /// <summary>
        /// The name of the application pool that the application within this site is running under.
        /// </summary>
        [DataMember]
        public string ApplicationPool { get; set; }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<IisSite> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Id)
                .ContainsProperty(p => p.Name)
                .ContainsProperty(p => p.Port)
                .ContainsProperty(p => p.State)
                .ContainsProperty(p => p.ApplicationPool);

            builder.Property(p => p.Id).ReadOnly();
            builder.Property(p => p.Name).ReadOnly();
            builder.Property(p => p.Port).ReadOnly();
            builder.Property(p => p.State).ReadOnly();
            builder.Property(p => p.ApplicationPool).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<IisSite> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);

            builder.Property(p => p.Port)
                .ColumnWidth(70);
            builder.Property(p => p.State)
                .FixedWidth()
                .ColumnWidth(100)
                .ReplaceEditor(new ComboEditorDefinition(typeof(ObjectState)));
            builder.Property(p => p.ApplicationPool)
                .FixedWidth()
                .ColumnWidth(150);
        }

        #endregion
    }
}
