using System;
using TIG.TotalLink.Shared.DataModel.Core.Enum;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Indicates the database domain that a data model belongs to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class DatabaseDomainAttribute : System.Attribute
    {
        #region Constructors

        public DatabaseDomainAttribute(DatabaseDomain domain = DatabaseDomain.Main)
        {
            Domain = domain;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The database domain that the data model belongs to.
        /// </summary>
        public DatabaseDomain Domain { get; private set; }

        #endregion
    }
}
