using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AppKit;
using CoreGraphics;
using Foundation;
using NUnit.Framework;
using ObjCRuntime;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class TreeAccessibilityTests
{
    [Test]
    public void NativeDisclosureExposesHierarchyAndPreservesChildrenAcrossCollapse()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var tree = new TreeView { Dock = DockStyle.Fill };
        var root = tree.Nodes.Add("Parent");
        root.Nodes.Add("Child").Nodes.Add("Grandchild");
        form.Controls.Add(tree);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tree.Handle);
            var parent = Item(view, "Parent");
            Assert.That(parent.AccessibilityRole, Is.EqualTo("AXRow"));
            Assert.That(parent.AccessibilityParent, Is.SameAs(view));
            Assert.That(parent.AccessibilityExpanded, Is.False);
            Assert.That(parent.AccessibilityChildren, Is.Empty);
            parent.AccessibilityExpanded = true;
            Assert.That(root.IsExpanded, Is.True);
            var child = Item(parent, "Child");
            Assert.That(child.AccessibilityParent, Is.SameAs(parent));
            Assert.That(child.AccessibilityChildren, Is.Empty);
            Assert.That(child.AccessibilityExpanded, Is.False);
            Assert.That((double)child.AccessibilityFrame.Width, Is.GreaterThan(0));
            Assert.That(HitTest(view, new CGPoint(child.AccessibilityFrame.GetMidX(), child.AccessibilityFrame.GetMidY())), Is.SameAs(child));
            parent.AccessibilityExpanded = false;
            Assert.That(child.AccessibilityHidden, Is.True);
            child.AccessibilitySelected = true;
            Assert.That(tree.SelectedNode, Is.Not.SameAs(root.Nodes[0]));
            Assert.That(child.AccessibilityFrame, Is.EqualTo(CGRect.Empty));
            Assert.That(parent.AccessibilityChildren, Is.Empty);
            parent.AccessibilityExpanded = true;
            Assert.That(Item(parent, "Child"), Is.SameAs(child));
            child.AccessibilitySelected = true;
            Assert.That(tree.SelectedNode, Is.SameAs(root.Nodes[0]));
            Assert.That(child.AccessibilitySelected, Is.True);
            child.AccessibilityFocused = true;
            Assert.That(child.AccessibilityFocused, Is.EqualTo(tree.ContainsFocus));
            Assert.That(view.AccessibilitySelectedRows, Does.Contain(child));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeHierarchyRefreshesAfterInsertAndIndexerReplacement()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var tree = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
        var parent = tree.Nodes.Add("Parent");
        parent.Nodes.Add("Original");
        form.Controls.Add(tree);
        try
        {
            form.Show();
            parent.Expand();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tree.Handle);
            var nativeParent = Item(view, "Parent");
            var original = Item(nativeParent, "Original");
            _ = view.AccessibilityRows;
            tree.Nodes.Insert(0, new TreeNode("First"));
            Assert.That(Item(view, "First").AccessibilityParent, Is.SameAs(view));
            parent.Nodes.Insert(0, new TreeNode("Inserted"));
            Assert.That(Item(nativeParent, "Inserted").AccessibilityParent, Is.SameAs(nativeParent));
            var replacement = new TreeNode("Replacement");
            parent.Nodes[1] = replacement;
            Assert.That(Item(nativeParent, "Replacement"), Is.Not.SameAs(original));
            Assert.That(original.AccessibilityHidden, Is.True);
            Assert.That(original.AccessibilityParent, Is.Null);
            original.AccessibilityValue = new NSNumber(true);
            Assert.That(original.AccessibilityPerformPress(), Is.False);
            Assert.That(replacement.Checked, Is.False);
            Assert.That(view.AccessibilityRows.OfType<NSAccessibilityElement>().Select(item => item.AccessibilityTitle),
                Is.EqualTo(new[] { "First", "Parent", "Inserted", "Replacement" }));
        }
        finally { form.Close(); }
    }

    [Test]
    public void DetachedNativeNodeCannotActOnReplacementAndSortingPreservesIdentity()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var tree = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
        var removed = tree.Nodes.Add("Zulu");
        tree.Nodes.Add("Alpha");
        form.Controls.Add(tree);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tree.Handle);
            var old = Item(view, "Zulu");
            tree.Sort();
            Assert.That(Item(view, "Zulu"), Is.SameAs(old));
            removed.Remove();
            var replacement = tree.Nodes.Add("Zulu");
            tree.SelectedNode = null;
            Assert.That(Item(view, "Zulu"), Is.Not.SameAs(old));
            old.AccessibilitySelected = true;
            old.AccessibilityValue = new NSNumber(true);
            old.AccessibilityExpanded = true;
            Assert.That(old.AccessibilityPerformPress(), Is.False);
            Assert.That(old.AccessibilityParent, Is.Null);
            Assert.That(old.AccessibilityHidden, Is.True);
            Assert.That(old.AccessibilityEnabled, Is.False);
            Assert.That(replacement.Checked, Is.False);
            Assert.That(tree.SelectedNode, Is.Null);
            removed.Text = "Reinserted";
            tree.Nodes.Add(removed);
            Assert.That(Item(view, "Reinserted"), Is.Not.SameAs(old));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeCheckboxActionsRespectManagedCancellationAndEnabledState()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var tree = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
        var node = tree.Nodes.Add("Option");
        form.Controls.Add(tree);
        try
        {
            form.Show();
            Application.DoEvents();
            var item = Item(Runtime.GetNSObject<NSView>(tree.Handle), "Option");
            Assert.That(((NSNumber)item.AccessibilityValue).BoolValue, Is.False);
            Assert.That(item.AccessibilityPerformPress(), Is.True);
            Assert.That(node.Checked, Is.True);
            item.AccessibilityValue = new NSNumber(false);
            Assert.That(node.Checked, Is.False);
            tree.BeforeCheck += (_, e) => e.Cancel = true;
            Assert.That(item.AccessibilityPerformPress(), Is.False);
            Assert.That(node.Checked, Is.False);
            tree.CheckBoxes = false;
            Assert.That(item.AccessibilityValue, Is.Null);
            item.AccessibilityValue = new NSNumber(true);
            Assert.That(node.Checked, Is.False);
            tree.SelectedNode = null;
            tree.Enabled = false;
            Assert.That(item.AccessibilityPerformPress(), Is.False);
            item.AccessibilitySelected = true;
            Assert.That(tree.SelectedNode, Is.Not.SameAs(node));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeDisclosureHonorsManagedCancellationAndDisposal()
    {
        using var form = new Form { ClientSize = new Size(400, 240) };
        using var tree = new TreeView { Dock = DockStyle.Fill };
        var node = tree.Nodes.Add("Parent");
        node.Nodes.Add("Child");
        form.Controls.Add(tree);
        try
        {
            form.Show();
            Application.DoEvents();
            var item = Item(Runtime.GetNSObject<NSView>(tree.Handle), "Parent");
            TreeViewCancelEventHandler cancel = (_, e) => e.Cancel = true;
            tree.BeforeExpand += cancel;
            item.AccessibilityExpanded = true;
            Assert.That(node.IsExpanded, Is.False);
            tree.BeforeExpand -= cancel;
            item.AccessibilityDisclosed = true;
            Assert.That(node.IsExpanded, Is.True);
            tree.BeforeCollapse += cancel;
            item.AccessibilityExpanded = false;
            Assert.That(node.IsExpanded, Is.True);
            tree.Dispose();
            Assert.That(item.AccessibilityHidden, Is.True);
            Assert.That(item.AccessibilityParent, Is.Null);
            Assert.That(item.AccessibilityPerformPress(), Is.False);
        }
        finally { form.Close(); }
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendHitTest(nint receiver, nint selector, CGPoint point);

    private static NSObject HitTest(NSView view, CGPoint point) =>
        Runtime.GetNSObject(SendHitTest(view.Handle, Selector.GetHandle("accessibilityHitTest:"), point));

    private static NSAccessibilityElement Item(NSObject parent, string title)
    {
        NSObject[] children = parent is NSView view ? view.AccessibilityChildren : ((NSAccessibilityElement)parent).AccessibilityChildren;
        return children.OfType<NSAccessibilityElement>().Single(item => item.AccessibilityTitle == title);
    }
}
