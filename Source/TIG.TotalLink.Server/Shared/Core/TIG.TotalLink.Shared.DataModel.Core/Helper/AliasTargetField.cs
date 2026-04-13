using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class AliasTargetField
    {
        #region Constructors

        public AliasTargetField(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the target field.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The Type of the target field.
        /// </summary>
        public Type Type { get; private set; }

        #endregion
    }
}
