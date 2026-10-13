using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public class DataGridViewTests
{
    [Test]
    public void ScrollingUpdatesDisplayedRowsWithoutMovingTheCurrentCellOrSelection()
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            grid.CurrentCell = grid[0, 2];
            grid.ClearSelection();
            grid.Rows[2].Selected = true;
            grid.Rows[4].Selected = true;
            var currentCell = grid.CurrentCell;
            var selectedRows = grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).OrderBy(index => index).ToArray();

            foreach (var target in new[] { 15, 0 })
            {
                grid.FirstDisplayedScrollingRowIndex = target;
                grid.Refresh();
                Application.DoEvents();

                Assert.That(grid.FirstDisplayedScrollingRowIndex, Is.EqualTo(target));
                Assert.That(grid.CurrentCell, Is.SameAs(currentCell));
                Assert.That(grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).OrderBy(index => index), Is.EqualTo(selectedRows));
                Assert.That(grid.Rows[target].Displayed, Is.True);
                Assert.That(grid.Rows[target == 0 ? 15 : 0].Displayed, Is.False);
                var rowBounds = grid.GetRowDisplayRectangle(target, false);
                Assert.That(rowBounds.Height, Is.EqualTo(grid.Rows[target].Height));
                Assert.That(grid.ClientRectangle.Contains(new Point(rowBounds.Left + 1, rowBounds.Top + 1)), Is.True);
            }
        }
        finally
        {
            form.Close();
        }
    }

    [TestCase(-1)]
    [TestCase(40)]
    public void InvalidRowIndexLeavesTheViewportAndCurrentCellUnchanged(int index)
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            grid.FirstDisplayedScrollingRowIndex = 10;
            grid.Refresh();
            Application.DoEvents();
            var currentCell = grid.CurrentCell;

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.FirstDisplayedScrollingRowIndex = index);
            Assert.That(grid.FirstDisplayedScrollingRowIndex, Is.EqualTo(10));
            Assert.That(grid.CurrentCell, Is.SameAs(currentCell));
            Assert.That(grid.Rows[10].Displayed, Is.True);
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void HiddenRowCannotBecomeTheFirstDisplayedRow()
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            grid.Rows[15].Visible = false;
            grid.FirstDisplayedScrollingRowIndex = 10;
            grid.Refresh();
            var currentCell = grid.CurrentCell;

            Assert.Throws<InvalidOperationException>(() => grid.FirstDisplayedScrollingRowIndex = 15);
            Assert.That(grid.FirstDisplayedScrollingRowIndex, Is.EqualTo(10));
            Assert.That(grid.CurrentCell, Is.SameAs(currentCell));
            Assert.That(grid.Rows[15].Displayed, Is.False);
        }
        finally
        {
            form.Close();
        }
    }

    private static DataGridView CreateGrid(Form form)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true
        };
        grid.Columns.Add("value", "Value");
        for (var index = 0; index < 40; index++)
            grid.Rows.Add($"Row {index}");
        form.Controls.Add(grid);
        return grid;
    }
}
