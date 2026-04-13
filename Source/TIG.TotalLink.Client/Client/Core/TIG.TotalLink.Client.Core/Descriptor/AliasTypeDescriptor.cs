using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Core.Descriptor
{
    public class AliasTypeDescriptor : CustomTypeDescriptor
    {
        #region Private Fields

        private readonly Type _objectType;
        private static readonly Dictionary<Type, CachedPropertyDescriptorCollection> _cachedProperties = new Dictionary<Type, CachedPropertyDescriptorCollection>();

        #endregion


        #region Constructors

        public AliasTypeDescriptor(ICustomTypeDescriptor parent, Type objectType)
            : base(parent)
        {
            _objectType = objectType;
        }

        #endregion


        #region Overrides

        public override PropertyDescriptorCollection GetProperties()
        {
            // Attempt to find cached properties for this type
            CachedPropertyDescriptorCollection cachedProperties;
            if (!_cachedProperties.TryGetValue(_objectType, out cachedProperties))
            {
                // If we didn't find any cached properties, then this is the first call for this type
                // Alias information will not be available yet so we will just cache the base properties
                cachedProperties = new CachedPropertyDescriptorCollection() { Properties = base.GetProperties() };
                _cachedProperties.Add(_objectType, cachedProperties);
            }
            else
            {
                // If we did find cached properties, but aliases haven't been generated yet and are available, generate and add them
                if (!cachedProperties.AreAliasesGenerated && DataModelHelper.AliasedFieldMappings.Any())
                {
                    var properties = DataModelHelper.AliasedFieldMappings
                        .Where(a => a.OwnerType == _objectType)
                        .Select(a => new DynamicPropertyDescriptor(_objectType, a.AliasFieldName, a.TargetFieldType, a.GetValue, a.SetValue, new DisplayAttribute() { AutoGenerateField = false }))
                        .Cast<PropertyDescriptor>()
                        .ToList();

                    cachedProperties.Properties = new PropertyDescriptorCollection(properties.Union(cachedProperties.Properties.Cast<PropertyDescriptor>()).ToArray());
                    cachedProperties.AreAliasesGenerated = true;
                }
            }

            // Return the properties
            return cachedProperties.Properties;
        }

        #endregion
    }
}
