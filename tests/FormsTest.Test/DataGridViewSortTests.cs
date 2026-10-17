using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class DataGridViewSortTests
{
    [TestCaseSource(nameof(NumericSortCases))]
    public void NumericSortPreservesTheFullValueOrderWhenRepeatedOrReversed(
        double[] values, double[] ascendingValues, string cultureName, ListSortDirection initialDirection)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

            using var grid = new DataGridView { AllowUserToAddRows = false };
            var column = new DataGridViewTextBoxColumn { Name = "value", ValueType = typeof(double) };
            grid.Columns.Add(column);
            foreach (var value in values)
                grid.Rows.Add(value);

            var oppositeDirection = initialDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            foreach (var direction in new[] { initialDirection, initialDirection, oppositeDirection, initialDirection })
            {
                grid.Sort(column, direction);

                var expected = direction == ListSortDirection.Ascending
                    ? ascendingValues
                    : ascendingValues.Reverse().ToArray();
                var actual = grid.Rows.Cast<DataGridViewRow>().Select(row => (double)row.Cells[0].Value).ToArray();
                Assert.That(actual, Is.EqualTo(expected), $"{direction} sorting under {cultureName} must preserve every value in numeric order.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    private static IEnumerable<TestCaseData> NumericSortCases()
    {
        var scenarios = new[]
        {
            (
                Name: "ClosePositiveValues",
                Values: new[] { 99.2, 99.1, 99.3 },
                AscendingValues: new[] { 99.1, 99.2, 99.3 }
            ),
            (
                Name: "NegativeAndFractionalValues",
                Values: new[] { 0.2, -1.1, -1.2, 0.1, -0.1, 0.0 },
                AscendingValues: new[] { -1.2, -1.1, -0.1, 0.0, 0.1, 0.2 }
            ),
            (
                Name: "LargeDifferences",
                Values: new[] { 4_000_000_000.0, 0.0, -4_000_000_000.0, 2_000_000_000.0 },
                AscendingValues: new[] { -4_000_000_000.0, 0.0, 2_000_000_000.0, 4_000_000_000.0 }
            ),
            (
                Name: "DuplicateValues",
                Values: new[] { 2.25, 1.25, 2.25, -1.25, 1.25 },
                AscendingValues: new[] { -1.25, 1.25, 1.25, 2.25, 2.25 }
            )
        };

        foreach (var scenario in scenarios)
        foreach (var cultureName in new[] { "de-DE", "en-US" })
        foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending })
        {
            yield return new TestCaseData(scenario.Values, scenario.AscendingValues, cultureName, direction)
                .SetName($"NumericSort_{scenario.Name}_{cultureName}_{direction}");
        }
    }
}
