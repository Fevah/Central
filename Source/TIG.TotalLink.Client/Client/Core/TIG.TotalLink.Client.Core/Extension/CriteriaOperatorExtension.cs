using System.Collections.Generic;
using DevExpress.Data.Filtering;
using TIG.TotalLink.Client.Core.Extension.CriteriaVisitor;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class CriteriaOperatorExtension
    {
        #region Private Fields

        private static RemoveInvalidPropertiesCriteriaVisitor _removeInvalidPropertiesVisitor;
        private static RemovePropertiesCriteriaVisitor _removePropertiesVisitor;
        private static ReplacePropertiesCriteriaVisitor _replacePropertiesVisitor;
        private static RemoveGroupedOperatorCriteriaVisitor _removeGroupedOperatorVisitor;
        private static ReplaceGroupedOperatorCriteriaVisitor _replaceGroupedOperatorVisitor;
        private static FindGroupedOperatorCriteriaVisitor _findGroupedOperatorVisitor;

        #endregion


        #region Private Properties

        /// <summary>
        /// A CriteriaVisitor for removing invalid properties.
        /// </summary>
        private static RemoveInvalidPropertiesCriteriaVisitor RemoveInvalidPropertiesVisitor
        {
            get
            {
                if (_removeInvalidPropertiesVisitor == null)
                    _removeInvalidPropertiesVisitor = new RemoveInvalidPropertiesCriteriaVisitor();

                return _removeInvalidPropertiesVisitor;
            }
        }

        /// <summary>
        /// A CriteriaVisitor for removing properties.
        /// </summary>
        private static RemovePropertiesCriteriaVisitor RemovePropertiesVisitor
        {
            get
            {
                if (_removePropertiesVisitor == null)
                    _removePropertiesVisitor = new RemovePropertiesCriteriaVisitor();

                return _removePropertiesVisitor;
            }
        }

        /// <summary>
        /// A CriteriaVisitor for replacing properties.
        /// </summary>
        private static ReplacePropertiesCriteriaVisitor ReplacePropertiesVisitor
        {
            get
            {
                if (_replacePropertiesVisitor == null)
                    _replacePropertiesVisitor = new ReplacePropertiesCriteriaVisitor();

                return _replacePropertiesVisitor;
            }
        }

        /// <summary>
        /// A CriteriaVisitor for removing an operator from groups.
        /// </summary>
        private static RemoveGroupedOperatorCriteriaVisitor RemoveGroupedOperatorVisitor
        {
            get
            {
                if (_removeGroupedOperatorVisitor == null)
                    _removeGroupedOperatorVisitor = new RemoveGroupedOperatorCriteriaVisitor();

                return _removeGroupedOperatorVisitor;
            }
        }

        /// <summary>
        /// A CriteriaVisitor for replacing an operator in groups.
        /// </summary>
        private static ReplaceGroupedOperatorCriteriaVisitor ReplaceGroupedOperatorVisitor
        {
            get
            {
                if (_replaceGroupedOperatorVisitor == null)
                    _replaceGroupedOperatorVisitor = new ReplaceGroupedOperatorCriteriaVisitor();

                return _replaceGroupedOperatorVisitor;
            }
        }

        /// <summary>
        /// A CriteriaVisitor for finding an operator in groups.
        /// </summary>
        private static FindGroupedOperatorCriteriaVisitor FindGroupedOperatorVisitor
        {
            get
            {
                if (_findGroupedOperatorVisitor == null)
                    _findGroupedOperatorVisitor = new FindGroupedOperatorCriteriaVisitor();

                return _findGroupedOperatorVisitor;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Removes all criteria that operate on properties that are not included in the <paramref name="validProperties"/> list.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to search for invalid properties in.</param>
        /// <param name="validProperties">A list of all valid property names.</param>
        /// <returns>A CriteriaOperator with all the invalid properties removed.</returns>
        public static CriteriaOperator RemoveInvalidProperties(this CriteriaOperator criteriaOperator, List<string> validProperties)
        {
            return RemoveInvalidPropertiesVisitor.Start(criteriaOperator, validProperties);
        }

        /// <summary>
        /// Removes all criteria that operate on properties that are included in the <paramref name="invalidProperties"/> list.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to search for invalid properties in.</param>
        /// <param name="invalidProperties">A list of all invalid property names.</param>
        /// <returns>A CriteriaOperator with all the invalid properties removed.</returns>
        public static CriteriaOperator RemoveProperties(this CriteriaOperator criteriaOperator, List<string> invalidProperties)
        {
            return RemovePropertiesVisitor.Start(criteriaOperator, invalidProperties);
        }

        /// <summary>
        /// Replaces all criteria that operate on properties that are included in the <paramref name="propertyReplacements"/> list.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to replace properties in.</param>
        /// <param name="propertyReplacements">A list of replacements to make.  The Key is the property to search for, and the Value is the the property to replace it with.</param>
        /// <returns>A CriteriaOperator with all the specified properties replaced.</returns>
        public static CriteriaOperator ReplaceProperties(this CriteriaOperator criteriaOperator, Dictionary<string, string> propertyReplacements)
        {
            return ReplacePropertiesVisitor.Start(criteriaOperator, propertyReplacements);
        }

        /// <summary>
        /// Removes any criteria that matches <paramref name="removeOperator"/>.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to remove the <paramref name="removeOperator"/> from.</param>
        /// <param name="removeOperator">The CriteriaOperator to remove.</param>
        /// <returns>A CriteriaOperator with the <paramref name="removeOperator"/> removed.</returns>
        public static CriteriaOperator RemoveOperator(this CriteriaOperator criteriaOperator, CriteriaOperator removeOperator)
        {
            if (Equals(criteriaOperator, removeOperator))
                return null;

            return RemoveGroupedOperatorVisitor.Start(criteriaOperator, removeOperator);
        }

        /// <summary>
        /// Replaces any criteria that matches <paramref name="removeOperator"/> with <paramref name="replaceOperator"/>.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to remove the <paramref name="removeOperator"/> from.</param>
        /// <param name="removeOperator">The CriteriaOperator to remove.</param>
        /// <param name="replaceOperator">The CriteriaOperator to replace the <paramref name="removeOperator"/> with.</param>
        /// <returns>A CriteriaOperator with the <paramref name="removeOperator"/> replaced.</returns>
        public static CriteriaOperator ReplaceOperator(this CriteriaOperator criteriaOperator, CriteriaOperator removeOperator, CriteriaOperator replaceOperator)
        {
            if (Equals(criteriaOperator, removeOperator))
                return CriteriaOperator.Clone(replaceOperator);

            return ReplaceGroupedOperatorVisitor.Start(criteriaOperator, removeOperator, replaceOperator);
        }

        /// <summary>
        /// Find any criteria that matches <paramref name="findOperator"/>.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to find the <paramref name="findOperator"/> in.</param>
        /// <param name="findOperator">The CriteriaOperator to find.</param>
        /// <returns>The matched CriteriaOperator if one was found; otherwise null.</returns>
        public static CriteriaOperator FindOperator(this CriteriaOperator criteriaOperator, CriteriaOperator findOperator)
        {
            if (Equals(criteriaOperator, findOperator))
                return criteriaOperator;

            return FindGroupedOperatorVisitor.Start(criteriaOperator, findOperator);
        }

        #endregion
    }
}
