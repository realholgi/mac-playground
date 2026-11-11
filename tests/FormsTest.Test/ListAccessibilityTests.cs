using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AppKit;
using Foundation;
using NUnit.Framework;
using ObjCRuntime;

namespace FormsTest.Test;

[TestFixture, NonParallelizable, SingleThreaded]
public sealed class ListAccessibilityTests
{
    [TestCase(View.LargeIcon)]
    [TestCase(View.SmallIcon)]
    [TestCase(View.List)]
    [TestCase(View.Tile)]
    public void NativeItemsExposeNamesSelectionFocusAndScreenBounds(View mode)
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var list = new ListView { Dock = DockStyle.Fill, View = mode, MultiSelect = false };
        list.Items.Add("First");
        list.Items.Add("Second");
        form.Controls.Add(list);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(list.Handle);
            var rows = view.AccessibilityChildren.OfType<NSAccessibilityElement>().Where(e => e.AccessibilityRole == "AXRow").ToArray();
            Assert.That(rows.Length, Is.EqualTo(2));
            Assert.That(rows[1].AccessibilityLabel, Is.EqualTo("Second"));
            Assert.That(rows[1].AccessibilityValue.ToString(), Is.EqualTo("Second"));
            Assert.That(rows[1].AccessibilityParent, Is.SameAs(view));
            Assert.That((double)rows[1].AccessibilityFrame.Width, Is.GreaterThan(0));
            Assert.That((double)rows[1].AccessibilityFrame.Height, Is.GreaterThan(0));
            rows[0].AccessibilitySelected = true;
            rows[1].AccessibilitySelected = true;
            Assert.That(list.Items[0].Selected, Is.False);
            Assert.That(list.Items[1].Selected, Is.True);
            rows[1].AccessibilityFocused = true;
            Assert.That(list.FocusedItem, Is.SameAs(list.Items[1]));
            Assert.That(view.AccessibilitySelectedChildren, Does.Contain(rows[1]));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeCheckboxActionChangesOnlyLiveEnabledItems()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var list = new ListView { Dock = DockStyle.Fill, CheckBoxes = true, View = View.List };
        list.Items.Add("Check me");
        form.Controls.Add(list);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(list.Handle);
            var item = view.AccessibilityChildren.OfType<NSAccessibilityElement>().Single(e => e.AccessibilityRole == "AXCheckBox");
            Assert.That(((NSNumber)item.AccessibilityValue).Int32Value, Is.Zero);
            Assert.That(item.AccessibilityPerformPress(), Is.True);
            Assert.That(list.Items[0].Checked, Is.True);
            Assert.That(((NSNumber)item.AccessibilityValue).Int32Value, Is.EqualTo(1));
            list.Enabled = false;
            Assert.That(item.AccessibilityPerformPress(), Is.False);
            Assert.That(list.Items[0].Checked, Is.True);
            list.Enabled = true;
            list.Items.RemoveAt(0);
            list.Items.Add("Replacement");
            item.AccessibilitySelected = true;
            Assert.That(item.AccessibilityPerformPress(), Is.False);
            Assert.That(item.AccessibilityHidden, Is.True);
            Assert.That(item.AccessibilityParent, Is.Null);
            Assert.That(list.Items[0].Checked, Is.False);
            Assert.That(list.Items[0].Selected, Is.False);
        }
        finally { form.Close(); }
    }

    [Test]
    public void DetailsExposeRealHeadersCellsAndStableIdentityAcrossSortingAndColumnReorder()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var list = new ListView { Dock = DockStyle.Fill, View = View.Details };
        list.Columns.Add("Name", 140);
        list.Columns.Add("Count", 90);
        list.Items.Add(new ListViewItem(new[] { "Zulu", "42" }));
        list.Items.Add(new ListViewItem(new[] { "Alpha", "7" }));
        form.Controls.Add(list);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(list.Handle);
            Assert.That(view.AccessibilityRowCount, Is.EqualTo((nint)2));
            Assert.That(view.AccessibilityColumnCount, Is.EqualTo((nint)2));
            Assert.That(((NSAccessibilityElement)view.AccessibilityColumnHeaderUIElements[1]).AccessibilityLabel, Is.EqualTo("Count"));
            var row = (NSAccessibilityElement)view.AccessibilityRows[0];
            var cell = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(1, 0);
            Assert.That(cell.AccessibilityRole, Is.EqualTo("AXCell"));
            Assert.That(cell.AccessibilityValue.ToString(), Is.EqualTo("42"));
            Assert.That(cell.AccessibilityParent, Is.SameAs(row));
            Assert.That(row.AccessibilityChildren, Does.Contain(cell));
            list.Items[0].SubItems[1].Text = "43";
            Assert.That(cell.AccessibilityValue.ToString(), Is.EqualTo("43"));
            list.Sorting = SortOrder.Ascending;
            list.Sort();
            Assert.That(view.AccessibilityRows[1], Is.SameAs(row));
            list.Columns[1].DisplayIndex = 0;
            Assert.That(view.GetAccessibilityCellForColumn(0, 1), Is.SameAs(cell));
            Assert.That(cell.AccessibilityColumnIndexRange.Location, Is.EqualTo((nint)0));
            Assert.That(cell.AccessibilityRowIndexRange.Location, Is.EqualTo((nint)1));
            list.Columns.RemoveAt(1);
            Assert.That(cell.AccessibilityHidden, Is.True);
            Assert.That(view.AccessibilityColumnCount, Is.EqualTo((nint)1));
        }
        finally { form.Close(); }
    }

    [Test]
    public void VirtualRowsAreLazyIndexIdentitiesAndRemovedIndicesCannotActOnRegrownList()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var list = new ListView { Dock = DockStyle.Fill, View = View.Details, VirtualMode = true };
        list.Columns.Add("Name", 140);
        int retrieved = 0;
        list.RetrieveVirtualItem += (_, e) => { ++retrieved; e.Item = new ListViewItem("Item " + e.ItemIndex); };
        list.VirtualListSize = 100000;
        form.Controls.Add(list);
        try
        {
            form.Show();
            Application.DoEvents();
            retrieved = 0;
            var view = Runtime.GetNSObject<NSView>(list.Handle);
            var visible = view.AccessibilityChildren;
            Assert.That(visible.Length, Is.LessThan(100));
            Assert.That(retrieved, Is.LessThan(100));
            Assert.That(view.AccessibilityRowCount, Is.EqualTo((nint)100000));
            var cell = (NSAccessibilityElement)view.GetAccessibilityCellForColumn(0, 90000);
            var row = (NSAccessibilityElement)cell.AccessibilityParent;
            Assert.That(view.GetAccessibilityCellForColumn(0, 90000), Is.SameAs(cell));
            Assert.That(cell.AccessibilityValue.ToString(), Is.EqualTo("Item 90000"));
            row.AccessibilitySelected = true;
            Assert.That(list.SelectedIndices.Contains(90000), Is.True);
            var survivor = view.GetAccessibilityCellForColumn(0, 5);
            list.VirtualListSize = 10;
            list.VirtualListSize = 100000;
            row.AccessibilitySelected = true;
            Assert.That(row.AccessibilityHidden, Is.True);
            Assert.That(list.SelectedIndices.Contains(90000), Is.False);
            Assert.That(view.GetAccessibilityCellForColumn(0, 90000), Is.Not.SameAs(cell));
            Assert.That(view.GetAccessibilityCellForColumn(0, 5), Is.SameAs(survivor));
        }
        finally { form.Close(); }
    }
}
