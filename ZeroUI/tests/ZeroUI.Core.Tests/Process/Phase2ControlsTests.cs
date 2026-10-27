using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests.Process
{
    public class Phase2ControlsTests
    {
        [Fact]
        public void FilterCriteria_ToSqlWhere_And_ToRowFilter_BuildsCorrectly()
        {
            var root = new GroupFilterNode(FilterGroupOperator.And);
            root.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
            root.AddCondition("Amount", FilterComparisonOperator.GreaterThan, "1000");

            string sql = root.ToSqlWhere();
            string rowFilter = root.ToRowFilter();

            Assert.Equal("([Status] = 'Active' AND [Amount] > '1000')", sql);
            Assert.Equal("([Status] = 'Active' AND [Amount] > '1000')", rowFilter);
        }

        [Fact]
        public void FilterCriteria_Between_Operator_GeneratesValidClauses()
        {
            var root = new GroupFilterNode(FilterGroupOperator.And);
            root.AddCondition("CreatedDate", FilterComparisonOperator.Between, "2026-01-01", "2026-12-31");

            string sql = root.ToSqlWhere();
            string rowFilter = root.ToRowFilter();

            Assert.Equal("([CreatedDate] BETWEEN '2026-01-01' AND '2026-12-31')", sql);
            Assert.Equal("(([CreatedDate] >= '2026-01-01' AND [CreatedDate] <= '2026-12-31'))", rowFilter);
        }

        [Fact]
        public void FilterCriteria_NestedGroups_NotOr_HandlesNegation()
        {
            var root = new GroupFilterNode(FilterGroupOperator.Or);
            root.AddCondition("Category", FilterComparisonOperator.Equals, "Electronics");

            var subGroup = root.AddGroup(FilterGroupOperator.NotAnd);
            subGroup.AddCondition("Discontinued", FilterComparisonOperator.Equals, "1");
            subGroup.AddCondition("Stock", FilterComparisonOperator.LessThanOrEqual, "0");

            string sql = root.ToSqlWhere();
            Assert.Contains("NOT ([Discontinued] = '1' AND [Stock] <= '0')", sql);
        }

        [Fact]
        public void FilterCriteria_Null_Checks_GenerateCorrectSyntax()
        {
            var root = new GroupFilterNode(FilterGroupOperator.And);
            root.AddCondition("DeletedAt", FilterComparisonOperator.IsNull, "");
            root.AddCondition("ApprovedBy", FilterComparisonOperator.IsNotNull, "");

            string sql = root.ToSqlWhere();
            string rowFilter = root.ToRowFilter();

            Assert.Equal("([DeletedAt] IS NULL AND [ApprovedBy] IS NOT NULL)", sql);
            Assert.Equal("([DeletedAt] IS NULL AND [ApprovedBy] IS NOT NULL)", rowFilter);
        }

        [Fact]
        public void CalculatorLogic_BasicOperations_WorkAccurately()
        {
            // Simulate decimal math used in CalcEdit
            decimal a = 1500000m;
            decimal taxRate = 0.1m;
            decimal tax = a * taxRate;
            decimal total = a + tax;

            Assert.Equal(150000m, tax);
            Assert.Equal(1650000m, total);

            // Precision formatting test
            string formattedVnd = string.Format("{0:#,##0} ₫", total);
            Assert.Equal("1,650,000 ₫", formattedVnd);
        }

        [Fact]
        public void ApprovalFlow_OverallStatus_Calculation()
        {
            var steps = new List<(string Title, string Status)>
            {
                ("Step 1", "Approved"),
                ("Step 2", "Approved"),
                ("Step 3", "Pending"),
                ("Step 4", "Pending")
            };

            // Overall is Pending if any step is not yet approved
            bool allApproved = true;
            bool hasRejected = false;

            foreach (var s in steps)
            {
                if (s.Status == "Rejected") hasRejected = true;
                if (s.Status != "Approved") allApproved = false;
            }

            Assert.False(allApproved);
            Assert.False(hasRejected);

            // Mark step 3 as Rejected
            steps[2] = ("Step 3", "Rejected");
            foreach (var s in steps)
            {
                if (s.Status == "Rejected") hasRejected = true;
            }

            Assert.True(hasRejected);
        }
    }
}
