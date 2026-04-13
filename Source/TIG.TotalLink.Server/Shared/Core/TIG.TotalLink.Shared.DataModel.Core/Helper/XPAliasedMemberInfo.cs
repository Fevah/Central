using System;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpo;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    // https://www.devexpress.com/Support/Center/Example/Details/E3473

    public class XPAliasedMemberInfo : XPMemberInfo
    {
        private readonly AliasedFieldMapping _alias;
        private readonly string _propertyName;
        private readonly Type _propertyType;

        public XPAliasedMemberInfo(XPClassInfo owner, string propertyName, Type propertyType, string expression)
            : base(owner, true)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName");

            _propertyName = propertyName;
            _propertyType = propertyType;

            Owner.AddMember(this);
            AddAttribute(new PersistentAliasAttribute(expression));
        }

        public XPAliasedMemberInfo(XPClassInfo owner, AliasedFieldMapping alias)
            : base(owner, true)
        {
            if (alias == null)
                throw new ArgumentNullException("alias");

            _alias = alias;
            _propertyName = alias.AliasFieldName;
            _propertyType = alias.TargetFieldType;

            Owner.AddMember(this);
            AddAttribute(new PersistentAliasAttribute(alias.AliasExpression));
        }

        /// <summary>
        /// The name of the property.
        /// </summary>
        public override string Name
        {
            get { return _propertyName; }
        }

        /// <summary>
        /// Indicates if the property is public.
        /// XPAliasedMemberInfo is always public, so this property always returns True.
        /// </summary>
        public override bool IsPublic
        {
            get { return true; }
        }

        /// <summary>
        /// The type of the property value.
        /// </summary>
        public override Type MemberType
        {
            get { return _propertyType; }
        }

        /// <summary>
        /// Indicates if the property can be persisted.
        /// XPAliasedMemberInfo is read-only, so this property always returns False.
        /// </summary>
        protected override bool CanPersist
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the value for a property.
        /// </summary>
        /// <param name="obj">The object to collect the property value from.</param>
        /// <returns>The value of the property from the specified object.</returns>
        public override object GetValue(object obj)
        {
            // If this member contains an alias,
            // and the alias collects data from a non-persistent field or a temporary value is stored for the alias,
            // call the AliasFieldMapping to get the value
            var aliasedDataObject = obj as IAliasedDataObject;
            if (_alias != null && (!_alias.IsPersistent || (aliasedDataObject != null && aliasedDataObject.ContainsAliasValue(_alias))))
                return _alias.GetValue(obj);

            // If this member collects data from a persistent field, use an ExpressionEvaluator to get the value
            var caseSensitive = XpoDefault.DefaultCaseSensitive;
            var sessionProvider = obj as ISessionProvider;
            if (sessionProvider != null)
                caseSensitive = sessionProvider.Session.CaseSensitive;

            var persistentAliasAttribute = (PersistentAliasAttribute)GetAttributeInfo(typeof(PersistentAliasAttribute));

            return new ExpressionEvaluator(Owner.GetEvaluatorContextDescriptor(),
                CriteriaOperator.Parse(persistentAliasAttribute.AliasExpression),
                caseSensitive, Owner.Dictionary.CustomFunctionOperators).Evaluate(obj);
        }

        /// <summary>
        /// Sets the value for a property.
        /// XPAliasedMemberInfo is read-only, so this method has no effect.
        /// </summary>
        /// <param name="obj">The object to set the value on.</param>
        /// <param name="newValue">The new value to apply.</param>
        public override void SetValue(object obj, object newValue)
        {
        }

        /// <summary>
        /// Indicates if this property has been modified.
        /// XPAliasedMemberInfo is read-only, so this method always returns False.
        /// </summary>
        /// <param name="obj">The object to return the modified state for.</param>
        /// <returns>True if the property is modified; otherwise false.</returns>
        public override bool GetModified(object obj)
        {
            return false;
        }

        /// <summary>
        /// Returns the old value for a property when it has been modified.
        /// XPAliasedMemberInfo is read-only, so this method returns the current value.
        /// </summary>
        /// <param name="obj">The object to return the old value for.</param>
        /// <returns>The old value of the property on the specified object.</returns>
        public override object GetOldValue(object obj)
        {
            return GetValue(obj);
        }

        /// <summary>
        /// Resets the modified flag for the property.
        /// XPAliasedMemberInfo is read-only, so this method has no effect.
        /// </summary>
        /// <param name="obj">The object to reset the modified flag on.</param>
        public override void ResetModified(object obj)
        {
        }

        /// <summary>
        /// Set the modified flag for the property if the current value differs from the supplied oldValue.
        /// XPAliasedMemberInfo is read-only, so this method has no effect.
        /// </summary>
        /// <param name="obj">The object to reset the modified flag on.</param>
        /// <param name="oldValue">The old value to compare to.</param>
        public override void SetModified(object obj, object oldValue)
        {
        }
    }
}
