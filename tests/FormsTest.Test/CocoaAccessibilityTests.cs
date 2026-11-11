using System.Drawing;
using System.Windows.Forms;
using AppKit;
using Foundation;
using NUnit.Framework;
using ObjCRuntime;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class CocoaAccessibilityTests
{
    [Test]
    public void SliderActionsClampAtBothBoundsAndNotifyOnlyOnChanges()
    {
        using var form = new Form();
        using var slider = new TrackBar { Minimum = -5, Maximum = 5, SmallChange = 3, Value = 4 };
        form.Controls.Add(slider);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(slider.Handle);
            int changes = 0;
            slider.ValueChanged += (_, _) => changes++;

            Assert.That(view.AccessibilityPerformIncrement(), Is.True);
            Assert.That(slider.Value, Is.EqualTo(5));
            Assert.That(view.AccessibilityPerformIncrement(), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            slider.Value = -4;
            changes = 0;
            Assert.That(view.AccessibilityPerformDecrement(), Is.True);
            Assert.That(slider.Value, Is.EqualTo(-5));
            Assert.That(view.AccessibilityPerformDecrement(), Is.False);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(((NSNumber)view.AccessibilityValue).Int32Value, Is.EqualTo(-5));
        }
        finally { form.Close(); }
    }

    [TestCase(-6)]
    [TestCase(6)]
    [TestCase(1.5)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void SliderRejectsInvalidNativeValues(double value)
    {
        using var form = new Form();
        using var slider = new TrackBar { Minimum = -5, Maximum = 5, Value = 2 };
        form.Controls.Add(slider);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(slider.Handle);
            int changes = 0;
            slider.ValueChanged += (_, _) => changes++;
            using var invalid = new NSNumber(value);
            view.AccessibilityValue = invalid;
            Assert.That(slider.Value, Is.EqualTo(2));
            Assert.That(changes, Is.Zero);
            using var valid = new NSNumber(-3);
            view.AccessibilityValue = valid;
            Assert.That(slider.Value, Is.EqualTo(-3));
            Assert.That(changes, Is.EqualTo(1));
        }
        finally { form.Close(); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnavailableSliderRejectsNativeAndManagedValueChanges(bool disable)
    {
        using var form = new Form();
        using var slider = new TrackBar { Value = 2 };
        form.Controls.Add(slider);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(slider.Handle);
            if (disable) slider.Enabled = false;
            else slider.Visible = false;
            using var value = new NSNumber(7);
            view.AccessibilityValue = value;
            slider.AccessibilityObject.Value = "8";
            Assert.That(view.AccessibilityPerformIncrement(), Is.False);
            Assert.That(view.AccessibilityPerformDecrement(), Is.False);
            Assert.That(slider.Value, Is.EqualTo(2));
            Assert.That(slider.AccessibilityObject.State & AccessibleStates.Unavailable, Is.Not.Zero);
        }
        finally { form.Close(); }
    }

    [Test]
    public void GridColumnVisibilityAndOrderPreserveSurvivingCellIdentity()
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        grid.Columns.Add("second", "Second");
        grid[1, 0].Value = "other";
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(grid.Handle);
            var first = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(0, 0);
            var second = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(1, 0);
            Assert.That(view.AccessibilityRowCount, Is.EqualTo((nint)2));
            Assert.That(view.AccessibilityColumnCount, Is.EqualTo((nint)2));
            Assert.That(second.AccessibilityValue.ToString(), Is.EqualTo("other"));

            grid.Columns[1].DisplayIndex = 0;
            Assert.That(view.GetAccessibilityCellForColumn(0, 0), Is.SameAs(second));
            Assert.That(view.GetAccessibilityCellForColumn(1, 0), Is.SameAs(first));
            grid.Columns[0].Visible = false;
            Assert.That(view.AccessibilityColumnCount, Is.EqualTo((nint)1));
            Assert.That(view.GetAccessibilityCellForColumn(0, 0), Is.SameAs(second));
            Assert.That(view.GetAccessibilityCellForColumn(1, 0), Is.Null);
            Assert.That(first.AccessibilityHidden, Is.True);
            Assert.That(first.AccessibilityEnabled, Is.False);
            Assert.That(grid.AccessibilityObject.GetChild(1).GetChild(1), Is.SameAs(grid[1, 0].AccessibilityObject));
        }
        finally { form.Close(); }
    }

    [Test]
    public void DetachedGridCellCannotEditOrSelectItsReplacement()
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(grid.Handle);
            var detached = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(0, 0);
            var managed = grid[0, 0].AccessibilityObject;
            grid.Rows.RemoveAt(0);
            var replacement = view.GetAccessibilityCellForColumn(0, 0);
            grid.ClearSelection();
            using var value = new NSString("corrupted");
            detached.AccessibilityValue = value;
            detached.AccessibilitySelected = true;
            managed.Value = "also corrupted";
            managed.Select(AccessibleSelection.TakeSelection);

            Assert.That(replacement, Is.Not.SameAs(detached));
            Assert.That(detached.AccessibilityHidden, Is.True);
            Assert.That(detached.AccessibilityParent, Is.Null);
            Assert.That(detached.AccessibilityPerformPress(), Is.False);
            Assert.That(managed.State & AccessibleStates.Unavailable, Is.Not.Zero);
            Assert.That(grid[0, 0].Value, Is.EqualTo("replacement"));
            Assert.That(grid.SelectedCells.Count, Is.Zero);
        }
        finally { form.Close(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeGridValueEditsRespectCellValidation(bool cancel)
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(grid.Handle);
            var cell = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(0, 0);
            object observed = null;
            object committedDuringValidation = null;
            grid.CellValidating += (_, e) =>
            {
                observed = e.FormattedValue;
                committedDuringValidation = grid[0, 0].Value;
                e.Cancel = cancel;
            };
            using var draft = new NSString("edited");
            cell.AccessibilityValue = draft;

            Assert.That(observed, Is.EqualTo("edited"));
            Assert.That(committedDuringValidation, Is.EqualTo("original"));
            Assert.That(grid[0, 0].Value, Is.EqualTo(cancel ? "original" : "edited"));
            Assert.That(grid[0, 0].IsInEditMode, Is.EqualTo(cancel));
            if (cancel) grid.CancelEdit();
        }
        finally { form.Close(); }
    }

    [TestCase("grid")]
    [TestCase("column")]
    [TestCase("row")]
    [TestCase("cell")]
    public void ReadOnlyGridCellsRejectNativeEditsAndDefaultActions(string scope)
    {
        using var form = new Form { ClientSize = new Size(400, 200) };
        using var grid = CreateGrid(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(grid.Handle);
            var cell = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(0, 0);
            switch (scope)
            {
                case "grid": grid.ReadOnly = true; break;
                case "column": grid.Columns[0].ReadOnly = true; break;
                case "row": grid.Rows[0].ReadOnly = true; break;
                case "cell": grid[0, 0].ReadOnly = true; break;
            }
            using var draft = new NSString("changed");
            cell.AccessibilityValue = draft;
            Assert.That(cell.AccessibilityPerformPress(), Is.False);
            Assert.That(grid[0, 0].Value, Is.EqualTo("original"));
            Assert.That(grid.IsCurrentCellInEditMode, Is.False);
            Assert.That(grid[0, 0].AccessibilityObject.State & AccessibleStates.ReadOnly, Is.Not.Zero);
        }
        finally { form.Close(); }
    }

    private static DataGridView CreateGrid(Form form)
    {
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        grid.Columns.Add("value", "Value");
        grid.Rows.Add("original");
        grid.Rows.Add("replacement");
        form.Controls.Add(grid);
        return grid;
    }
}
