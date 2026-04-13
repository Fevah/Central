using System;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Defines a class as a document data model.
    /// Should be applied to viewmodels that will be used to initialize documents.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DocumentDataModelAttribute : System.Attribute
    {
        #region Constructors

        public DocumentDataModelAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Name of the model.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Short description of the model.
        /// </summary>
        public string Description { get; private set; }
        
        #endregion
    }
}
