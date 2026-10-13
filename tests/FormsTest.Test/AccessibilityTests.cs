using System;
using System.Drawing;
using System.Windows.Forms;
using Accessibility;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public class AccessibilityTests
{
    [Test]
    public void ChildIdsExposeMetadataAndScreenBoundsAndFollowChildRemoval()
    {
        using var form = new Form { ClientSize = new Size(400, 200), AccessibleName = "Window" };
        using var first = new TextBox { Bounds = new Rectangle(20, 30, 120, 24), AccessibleName = "First", AccessibleDescription = "Account name", AccessibleRole = AccessibleRole.Text };
        using var second = new Button { Bounds = new Rectangle(20, 70, 100, 30), AccessibleName = "Second" };
        form.Controls.AddRange([first, second]);
        try
        {
            form.Show();
            Application.DoEvents();
            var accessible = (IAccessible)form.AccessibilityObject;

            Assert.That(accessible.accChildCount, Is.EqualTo(2));
            Assert.That(accessible.get_accName(0), Is.EqualTo("Window"));
            Assert.That(accessible.get_accName(1), Is.EqualTo("First"));
            Assert.That(accessible.get_accName(2), Is.EqualTo("Second"));
            Assert.That(accessible.get_accDescription(1), Is.EqualTo("Account name"));
            Assert.That(accessible.get_accRole(1), Is.EqualTo((int)AccessibleRole.Text));
            accessible.accLocation(out var x, out var y, out var width, out var height, 1);
            Assert.That(new Rectangle(x, y, width, height), Is.EqualTo(form.RectangleToScreen(first.Bounds)));

            accessible.set_accName(1, "Renamed");
            Assert.That(first.AccessibilityObject.Name, Is.EqualTo("Renamed"));
            Assert.That(accessible.get_accName(2), Is.EqualTo("Second"));
            form.Controls.Remove(first);
            Assert.That(accessible.accChildCount, Is.EqualTo(1));
            Assert.That(accessible.get_accName(1), Is.EqualTo("Second"));
            Assert.Throws<ArgumentException>(() => accessible.get_accName(2));
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void ChildDefaultActionTogglesTheCheckboxAndSelectionMovesFocus()
    {
        using var form = new Form { ClientSize = new Size(300, 150) };
        using var checkBox = new CheckBox { Bounds = new Rectangle(10, 10, 150, 30), Text = "Enable" };
        using var textBox = new TextBox { Bounds = new Rectangle(10, 50, 150, 24) };
        form.Controls.AddRange([checkBox, textBox]);
        try
        {
            form.Show();
            AppKit.NSApplication.SharedApplication.Activate();
            form.Activate();
            Assert.That(() =>
            {
                Application.DoEvents();
                return AppKit.NSApplication.SharedApplication.Active && form.ContainsFocus;
            }, Is.True.After(2000, 10), "The test window must own native focus before selecting an accessible child.");
            var accessible = (IAccessible)form.AccessibilityObject;

            accessible.accDoDefaultAction(1);
            Assert.That(checkBox.Checked, Is.True);
            Assert.That((int)accessible.get_accState(1) & (int)AccessibleStates.Checked, Is.Not.Zero);
            accessible.accDoDefaultAction(1);
            Assert.That(checkBox.Checked, Is.False);
            Assert.That((int)accessible.get_accState(1) & (int)AccessibleStates.Checked, Is.Zero);

            accessible.accSelect((int)AccessibleSelection.TakeFocus, 2);
            Application.DoEvents();
            Assert.That(textBox.Focused, Is.True);
            Assert.That(accessible.accFocus, Is.SameAs(textBox.AccessibilityObject));
            foreach (var invalid in new object[] { -1, 3, "1", null })
                Assert.Throws<ArgumentException>(() => accessible.accDoDefaultAction(invalid));
            Assert.That(checkBox.Checked, Is.False);
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void OwnerlessAccessibleObjectReportsNoChildrenFocusOrGeometry()
    {
        var accessible = (IAccessible)new AccessibleObject();
        Assert.That(accessible.accChildCount, Is.Zero);
        Assert.That(accessible.accFocus, Is.Null);
        Assert.That(accessible.accParent, Is.Null);
        Assert.That(accessible.accSelection, Is.Null);
        accessible.accLocation(out var x, out var y, out var width, out var height, 0);
        Assert.That(new Rectangle(x, y, width, height), Is.EqualTo(Rectangle.Empty));
        Assert.That(accessible.accHitTest(100, 100), Is.Null);
        Assert.That(accessible.accNavigate((int)AccessibleNavigation.FirstChild, 0), Is.Null);
        Assert.Throws<ArgumentException>(() => accessible.get_accChild(1));
    }
}
