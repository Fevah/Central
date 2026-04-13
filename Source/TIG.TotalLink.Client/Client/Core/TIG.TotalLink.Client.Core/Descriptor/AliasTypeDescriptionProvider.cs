using System;
using System.ComponentModel;

namespace TIG.TotalLink.Client.Core.Descriptor
{
    public class AliasTypeDescriptionProvider : TypeDescriptionProvider
    {
        #region Constructors

        public AliasTypeDescriptionProvider(TypeDescriptionProvider parent)
            : base(parent)
        {
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Create and return our custom type descriptor and chain it with the original custom type descriptor.
        /// </summary>
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            return new AliasTypeDescriptor(base.GetTypeDescriptor(objectType, instance), objectType);
        }

        #endregion
    }
}
