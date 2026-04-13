using System;
using DevExpress.Xpo.Metadata;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class XPClassInfoExtension
    {
        // https://www.devexpress.com/Support/Center/Example/Details/E3473

        #region Public Methods

        /// <summary>
        /// Creates an aliased member based on the supplied parameters.
        /// </summary>
        /// <param name="self">The ClassInfo that the member will be added to.</param>
        /// <param name="name">The name of the new member.</param>
        /// <param name="type">The type of the new member.</param>
        /// <param name="expression">The expression for the new member.</param>
        /// <returns>A new XPAliasedMemberInfo.</returns>
        public static XPAliasedMemberInfo CreateAliasedMember(this XPClassInfo self, string name, Type type, string expression)
        {
            return CreateAliasedMember(self, name, type, expression, null);
        }

        /// <summary>
        /// Creates an aliased member based on the supplied parameters.
        /// </summary>
        /// <param name="self">The ClassInfo that the member will be added to.</param>
        /// <param name="name">The name of the new member.</param>
        /// <param name="type">The type of the new member.</param>
        /// <param name="expression">The expression for the new member.</param>
        /// <param name="attributes">An array of attributes to add to the new member.</param>
        /// <returns>A new XPAliasedMemberInfo.</returns>
        public static XPAliasedMemberInfo CreateAliasedMember(this XPClassInfo self, string name, Type type, string expression, params System.Attribute[] attributes)
        {
            var result = new XPAliasedMemberInfo(self, name, type, expression);
            if (attributes != null)
            {
                foreach (var a in attributes)
                {
                    result.AddAttribute(a);
                }
            }
            return result;
        }

        /// <summary>
        /// Creates an aliased member based on the supplied AliasedFieldMapping.
        /// </summary>
        /// <param name="self">The ClassInfo that the member will be added to.</param>
        /// <param name="alias">The AliasedFieldMapping to create the new member from.</param>
        /// <returns>A new XPAliasedMemberInfo.</returns>
        public static XPAliasedMemberInfo CreateAliasedMember(this XPClassInfo self, AliasedFieldMapping alias)
        {
            return CreateAliasedMember(self, alias, null);
        }

        /// <summary>
        /// Creates an aliased member based on the supplied AliasedFieldMapping.
        /// </summary>
        /// <param name="self">The ClassInfo that the member will be added to.</param>
        /// <param name="alias">The AliasedFieldMapping to create the new member from.</param>
        /// <param name="attributes">An array of attributes to add to the new member.</param>
        /// <returns>A new XPAliasedMemberInfo.</returns>
        public static XPAliasedMemberInfo CreateAliasedMember(this XPClassInfo self, AliasedFieldMapping alias, params System.Attribute[] attributes)
        {
            var result = new XPAliasedMemberInfo(self, alias);
            if (attributes != null)
            {
                foreach (var a in attributes)
                {
                    result.AddAttribute(a);
                }
            }
            return result;
        }

        #endregion
    }
}
