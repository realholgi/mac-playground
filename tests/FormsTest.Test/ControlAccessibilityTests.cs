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
public sealed class ControlAccessibilityTests
{
    [Test]
    public void CheckboxNativePressUsesNormalThreeStateClickPipeline()
    {
        using var form = new Form();
        using var check = new CheckBox { ThreeState = true, Text = "Option" };
        form.Controls.Add(check);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(check.Handle);
            int clicks = 0;
            check.Click += (_, _) => clicks++;
            foreach (var expected in new[] { CheckState.Indeterminate, CheckState.Checked, CheckState.Unchecked })
            {
                Assert.That(view.AccessibilityPerformPress(), Is.True);
                Assert.That(check.CheckState, Is.EqualTo(expected));
                Assert.That(((NSNumber)view.AccessibilityValue).Int32Value, Is.EqualTo((int)expected));
            }
            Assert.That(clicks, Is.EqualTo(3));
            check.AutoCheck = false;
            Assert.That(view.AccessibilityPerformPress(), Is.True);
            Assert.That(check.CheckState, Is.EqualTo(CheckState.Unchecked));
            Assert.That(clicks, Is.EqualTo(4));
        }
        finally { form.Close(); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnavailableCheckboxRejectsNativePress(bool disable)
    {
        using var form = new Form();
        using var check = new CheckBox();
        form.Controls.Add(check);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(check.Handle);
            int clicks = 0;
            check.Click += (_, _) => clicks++;
            if (disable) check.Enabled = false;
            else check.Visible = false;
            Assert.That(view.IsAccessibilitySelectorAllowed(new Selector("accessibilityPerformPress")), Is.False);
            Assert.That(view.AccessibilityPerformPress(), Is.False);
            Assert.That(check.Checked, Is.False);
            Assert.That(clicks, Is.Zero);
        }
        finally { form.Close(); }
    }

    [Test]
    public void RadioNativePressSelectsOnlyOneSiblingAndRaisesClick()
    {
        using var form = new Form();
        using var first = new RadioButton { Checked = true };
        using var second = new RadioButton { Location = new Point(0, 40) };
        form.Controls.AddRange([first, second]);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(second.Handle);
            int clicks = 0;
            second.Click += (_, _) => clicks++;
            Assert.That(view.AccessibilityPerformPress(), Is.True);
            Assert.That(second.Checked, Is.True);
            Assert.That(first.Checked, Is.False);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(((NSNumber)view.AccessibilityValue).Int32Value, Is.EqualTo(1));
        }
        finally { form.Close(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeLabelsKeepNamesSeparateFromHelpAndFollowChanges(bool nativeButton)
    {
        using var form = new Form();
        using Control control = nativeButton ? new Button() : new CheckBox();
        control.Text = "&Save && close";
        control.AccessibleDescription = "Writes the current document";
        form.Controls.Add(control);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(control.Handle);
            Assert.That(view.AccessibilityLabel, Is.EqualTo("Save & close"));
            Assert.That(view.AccessibilityHelp, Is.EqualTo("Writes the current document"));
            control.AccessibleName = "Store document";
            Assert.That(view.AccessibilityLabel, Is.EqualTo("Store document"));
            control.AccessibleDescription = "Keeps the editor open";
            Assert.That(view.AccessibilityHelp, Is.EqualTo("Keeps the editor open"));
            control.Text = "Changed caption";
            Assert.That(view.AccessibilityLabel, Is.EqualTo("Store document"));
            control.AccessibleName = null;
            Assert.That(view.AccessibilityLabel, Is.EqualTo("Changed caption"));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeMetadataFollowsAccessibleObjectOverridesAndHandleRecreation()
    {
        using var form = new Form();
        using var button = new RecreatedButton { Text = "Caption", AccessibleDescription = "Initial help" };
        form.Controls.Add(button);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(button.Handle);
            button.AccessibilityObject.Name = "Overridden name";
            Assert.That(view.AccessibilityLabel, Is.EqualTo("Overridden name"));
            button.Recreate();
            var recreated = Runtime.GetNSObject<NSView>(button.Handle);
            Assert.That(recreated.AccessibilityLabel, Is.EqualTo("Overridden name"));
            button.AccessibleDescription = "Updated help";
            Assert.That(recreated.AccessibilityHelp, Is.EqualTo("Updated help"));
            button.Text = "New caption";
            Assert.That(recreated.AccessibilityLabel, Is.EqualTo("Overridden name"));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeContainerChildrenFollowNestedAddRemoveAndVisibilityChanges()
    {
        using var form = new Form();
        using var outer = new Panel { Dock = DockStyle.Fill };
        using var inner = new Panel { Dock = DockStyle.Fill };
        using var first = new Button { Text = "First" };
        using var second = new Button { Text = "Second", Location = new Point(0, 40) };
        outer.Controls.Add(inner);
        inner.Controls.Add(first);
        form.Controls.Add(outer);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(outer.Handle);
            var firstElement = NSAccessibility.GetUnignoredDescendant(Runtime.GetNSObject<NSView>(first.Handle));
            Assert.That(view.AccessibilityChildren, Does.Contain(firstElement));
            inner.Controls.Add(second);
            var secondElement = NSAccessibility.GetUnignoredDescendant(Runtime.GetNSObject<NSView>(second.Handle));
            Assert.That(view.AccessibilityChildren, Does.Contain(firstElement).And.Contain(secondElement));
            first.Visible = false;
            Assert.That(view.AccessibilityChildren, Does.Not.Contain(firstElement));
            first.Visible = true;
            Assert.That(view.AccessibilityChildren, Does.Contain(firstElement));
            inner.Controls.Remove(second);
            Assert.That(view.AccessibilityChildren, Does.Not.Contain(secondElement));
        }
        finally { form.Close(); }
    }

    private sealed class RecreatedButton : Button
    {
        public void Recreate() => RecreateHandle();
    }
}
