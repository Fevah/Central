using System;
using System.Linq;
using System.Reflection;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class AliasedFieldMapping
    {
        #region Constructors

        /// <summary>
        /// Constructs an AliasedFieldMapping where the aliased field displays a property of the source object.
        /// </summary>
        /// <param name="ownerType">The Type of entity that this alias applies to.</param>
        /// <param name="declaringType">The Type of entity that declares this alias.</param>
        /// <param name="sourceFieldName">The name of the source field that is being aliased.</param>
        /// <param name="sourceFieldType">The Type that is returned by the SourceFieldName.</param>
        /// <param name="targetFields">Details of the fields that must be traversed to reach the value that will be displayed by the alias.</param>
        public AliasedFieldMapping(Type ownerType, Type declaringType, string sourceFieldName, Type sourceFieldType, params AliasTargetField[] targetFields)
        {
            if (targetFields == null || targetFields.Length == 0)
                throw new ArgumentException("targetFields cannot be null or empty.", "targetFields");

            OwnerType = ownerType;
            DeclaringType = declaringType;
            SourceFieldName = sourceFieldName;
            SourceFieldType = sourceFieldType;
            TargetFields = targetFields;
            TargetFieldType = targetFields[targetFields.Length - 1].Type;

            AliasFieldName = string.Format("{0}_Display", SourceFieldName);
            AliasExpression = string.Join(".", (new[] { sourceFieldName }).Concat(targetFields.Select(f => f.Name)));

            var sourceProperty = ownerType.GetProperty(sourceFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IsPersistent = (sourceProperty != null && sourceProperty.GetCustomAttribute<NonPersistentAttribute>(true) == null);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Type of entity that this alias applies to.
        /// </summary>
        public Type OwnerType { get; private set; }

        /// <summary>
        /// The base Type that declares this property.
        /// </summary>
        public System.Type DeclaringType { get; private set; }

        /// <summary>
        /// The name of the source field that is being aliased.
        /// </summary>
        public string SourceFieldName { get; private set; }

        /// <summary>
        /// The Type that is returned by the SourceFieldName.
        /// </summary>
        public Type SourceFieldType { get; private set; }

        /// <summary>
        /// Details of the fields that must be traversed to reach the value that will be displayed by the alias.
        /// </summary>
        public AliasTargetField[] TargetFields { get; private set; }

        /// <summary>
        /// The Type that will be returned by the last target field.
        /// </summary>
        public Type TargetFieldType { get; private set; }

        /// <summary>
        /// The new name for the aliased field.
        /// </summary>
        public string AliasFieldName { get; private set; }

        /// <summary>
        /// The new expression for the aliased field.
        /// </summary>
        public string AliasExpression { get; private set; }

        /// <summary>
        /// Indicates if the SourceFieldType is a persistent object.
        /// </summary>
        public bool IsPersistent { get; private set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns the related value for an alias.
        /// </summary>
        /// <param name="owner">The object to collect the alias value from.</param>
        /// <returns>The value of the aliased property.</returns>
        public object GetValue(object owner)
        {
            // Attempt to get the owner as an IAliasedDataObject
            var aliasedDataObject = owner as IAliasedDataObject;
            if (aliasedDataObject != null)
            {
                // If the owner is an IAliasedDataObject, and it contains a value for this alias, return the temporary alias value
                object value;
                if (aliasedDataObject.AliasValues.TryGetValue(AliasFieldName, out value))
                    return value;
            }

            // Attempt to get the sourceProperty from the component
            var sourceProperty = owner.GetType().GetProperty(SourceFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sourceProperty == null)
                return null;

            // Attempt to get the value of the sourceProperty from the component
            var sourceObject = sourceProperty.GetValue(owner);
            if (sourceObject == null)
                return null;

            // Iterate through all the TargetFields to find the final value
            var currentValue = sourceObject;
            foreach (var targetField in TargetFields)
            {
                // Attempt to get the targetProperty from the currentValue
                var targetProperty = sourceObject.GetType().GetProperty(targetField.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetProperty == null)
                    return null;

                // Attempt to get the value of the targetProperty from the currentValue
                currentValue = targetProperty.GetValue(currentValue);
                if (currentValue == null)
                    return null;
            }

            return currentValue;
        }

        /// <summary>
        /// Stores the related value for an alias.
        /// Note that this value will be temporarily stored in IAliasedDataObject.AliasValues
        /// and is used solely for the purpose of displaying alias values on cloned or copied data objects,
        /// so it will never be persisted.
        /// </summary>
        /// <param name="owner">The object to store the alias value on.</param>
        /// <param name="value">The new value for the alias.</param>
        public void SetValue(object owner, object value)
        {
            // Attempt to get the owner as an IAliasedDataObject
            var aliasedDataObject = owner as IAliasedDataObject;
            if (aliasedDataObject == null)
                return;

            // Store the value in AliasValues
            aliasedDataObject.AliasValues[AliasFieldName] = value;
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} = {1}", SourceFieldName, string.Join(", ", TargetFields.Select(t => t.Name)));
        }

        #endregion
    }
}
