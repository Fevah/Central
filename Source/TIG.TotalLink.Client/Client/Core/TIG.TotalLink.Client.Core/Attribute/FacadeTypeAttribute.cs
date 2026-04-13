using System;

namespace TIG.TotalLink.Client.Core.Attribute
{
    /// <summary>
    /// Defines the type of facade that manages a data object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class FacadeTypeAttribute : System.Attribute
    {
        #region Constructors

        public FacadeTypeAttribute(Type facadeType)
        {
            FacadeType = facadeType;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type of facade that manages the data object.
        /// </summary>
        public Type FacadeType { get; private set; }

        #endregion
    }
}
