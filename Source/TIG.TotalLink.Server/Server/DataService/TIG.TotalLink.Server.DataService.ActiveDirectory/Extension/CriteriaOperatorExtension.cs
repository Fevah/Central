using DevExpress.Data.Filtering;
using TIG.TotalLink.Server.DataService.ActiveDirectory.Extension.CriteriaVisitor;

namespace TIG.TotalLink.Server.DataService.ActiveDirectory.Extension
{
    public static class CriteriaOperatorExtension
    {
        #region Private Fields

        private static ConvertQueryCriteriaVisitor _convertQueryVisitor;

        #endregion


        #region Private Properties

        /// <summary>
        /// A CriteriaVisitor for replacing query operands.
        /// </summary>
        private static ConvertQueryCriteriaVisitor ConvertQueryVisitor
        {
            get
            {
                if (_convertQueryVisitor == null)
                    _convertQueryVisitor = new ConvertQueryCriteriaVisitor();

                return _convertQueryVisitor;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Replaces all query operands in the criteria with standard operands.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to replace query operands in.</param>
        /// <returns>A CriteriaOperator with all the query operands replaced.</returns>
        public static CriteriaOperator ConvertQuery(this CriteriaOperator criteriaOperator)
        {
            return ConvertQueryVisitor.Start(criteriaOperator);
        }

        #endregion
    }
}