using System.Collections.Generic;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;

namespace TIG.TotalLink.Server.DataService.ActiveDirectory.Extension.CriteriaVisitor
{
    public class ConvertQueryCriteriaVisitor : IQueryCriteriaVisitor<CriteriaOperator>
    {
        #region Public Methods

        /// <summary>
        /// Starts executing this CriteriaVisitor.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to replace query operands in.</param>
        /// <returns>A CriteriaOperator with all the query operands replaced.</returns>
        public CriteriaOperator Start(CriteriaOperator criteriaOperator)
        {
            // Execute the CriteriaVisitor
            return criteriaOperator.Accept(this);
        }

        #endregion


        #region IQueryCriteriaVisitor

        public CriteriaOperator Visit(BetweenOperator theOperator)
        {
            var test = theOperator.TestExpression.Accept(this);
            var begin = theOperator.BeginExpression.Accept(this);
            var end = theOperator.EndExpression.Accept(this);
            if (ReferenceEquals(test, null) || ReferenceEquals(begin, null) || ReferenceEquals(end, null))
                return null;

            return new BetweenOperator(test, begin, end);
        }

        public CriteriaOperator Visit(BinaryOperator theOperator)
        {
            var leftOperand = theOperator.LeftOperand.Accept(this);
            var rightOperand = theOperator.RightOperand.Accept(this);
            if (ReferenceEquals(leftOperand, null) || ReferenceEquals(rightOperand, null))
                return null;

            return new BinaryOperator(leftOperand, rightOperand, theOperator.OperatorType);
        }

        public CriteriaOperator Visit(UnaryOperator theOperator)
        {
            var operand = theOperator.Operand.Accept(this);
            if (ReferenceEquals(operand, null))
                return null;

            return new UnaryOperator(theOperator.OperatorType, operand);
        }

        public CriteriaOperator Visit(InOperator theOperator)
        {
            var leftOperand = theOperator.LeftOperand.Accept(this);
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    continue;

                operators.Add(temp);
            }

            if (ReferenceEquals(leftOperand, null))
                return null;

            return new InOperator(leftOperand, operators);
        }

        public CriteriaOperator Visit(GroupOperator theOperator)
        {
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    continue;

                operators.Add(temp);
            }

            return new GroupOperator(theOperator.OperatorType, operators);
        }

        public CriteriaOperator Visit(OperandValue theOperand)
        {
            return theOperand;
        }

        public CriteriaOperator Visit(FunctionOperator theOperator)
        {
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    return null;

                operators.Add(temp);
            }

            return new FunctionOperator(theOperator.OperatorType, operators);
        }

        public CriteriaOperator Visit(QueryOperand theOperand)
        {
            // Remove operands which operate on GCRecord, because GCRecord is always null and therefore will have no effect on the result
            if (theOperand.ColumnName == "GCRecord")
                return null;

            // Convert the QueryOperand to an OperandProperty
            return new OperandProperty(theOperand.ColumnName);
        }

        public CriteriaOperator Visit(QuerySubQueryContainer theOperand)
        {
            // Remove QuerySubQueryContainer because it is not supported
            return null;
        }

        #endregion
    }
}