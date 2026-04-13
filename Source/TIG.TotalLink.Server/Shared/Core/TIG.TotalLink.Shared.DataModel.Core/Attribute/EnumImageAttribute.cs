using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Apply to an enum value to specify an image to display in editors which inherit from EnumEditorDefinitionBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnumImageAttribute : System.Attribute
    {
        #region Constructors

        public EnumImageAttribute(string imageUri)
        {
            ImageUri = imageUri;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Absolute uri of the image to display.
        /// </summary>
        public string ImageUri { get; set; }

        #endregion
    }
}
