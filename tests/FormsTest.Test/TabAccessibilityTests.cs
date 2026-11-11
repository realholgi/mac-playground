using System;
using System.Linq;
using System.Windows.Forms;
using AppKit;
using Foundation;
using NUnit.Framework;
using ObjCRuntime;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class TabAccessibilityTests
{
    [Test]
    public void NativeTabPressSelectsThroughNormalEventsAndReportsSelection()
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tabs.Handle);
            var items = Items(view);
            int selecting = 0, selected = 0, changed = 0;
            tabs.Selecting += (_, _) => selecting++;
            tabs.Selected += (_, _) => selected++;
            tabs.SelectedIndexChanged += (_, _) => changed++;

            Assert.That(view.AccessibilityRole, Is.EqualTo("AXTabGroup"));
            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items[0].AccessibilitySelected, Is.True);
            Assert.That(items[1].AccessibilityRole, Is.EqualTo("AXRadioButton"));
            Assert.That(items[1].AccessibilityPerformPress(), Is.True);
            Assert.That(tabs.SelectedIndex, Is.EqualTo(1));
            Assert.That(selecting, Is.EqualTo(1));
            Assert.That(selected, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(items[0].AccessibilitySelected, Is.False);
            Assert.That(items[1].AccessibilitySelected, Is.True);
            Assert.That(((NSNumber)items[1].AccessibilityValue).Int32Value, Is.EqualTo(1));
            Assert.That(items[1].AccessibilityParent, Is.SameAs(view));
            Assert.That((double)items[1].AccessibilityFrame.Width, Is.GreaterThan(0));
            Assert.That((double)items[1].AccessibilityFrame.Height, Is.GreaterThan(0));
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeTabFocusSelectsThePageWithoutStealingItsEditorsAccessibility()
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        using var editor = new TextBox();
        tabs.TabPages[1].Controls.Add(editor);
        try
        {
            form.Show();
            NSApplication.SharedApplication.Activate();
            form.Activate();
            tabs.Focus();
            Assert.That(() =>
            {
                Application.DoEvents();
                return NSApplication.SharedApplication.Active && tabs.Focused;
            }, Is.True.After(2000, 10), "The tab strip must own native focus before testing accessibility focus.");
            Application.DoEvents();
            var items = Items(Runtime.GetNSObject<NSView>(tabs.Handle));
            items[1].AccessibilityFocused = true;
            Assert.That(tabs.SelectedIndex, Is.EqualTo(1));
            Assert.That(tabs.Focused, Is.True);
            Assert.That(items[1].AccessibilityFocused, Is.True);
            Assert.That(items[0].AccessibilityFocused, Is.False);
            editor.Focus();
            Assert.That(items[1].AccessibilityFocused, Is.False);
            Assert.That(editor.Focused, Is.True);
        }
        finally { form.Close(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeTabPressHonorsSelectionCancellation(bool cancelDeselecting)
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var items = Items(Runtime.GetNSObject<NSView>(tabs.Handle));
            int changes = 0;
            tabs.SelectedIndexChanged += (_, _) => changes++;
            if (cancelDeselecting) tabs.Deselecting += (_, e) => e.Cancel = true;
            else tabs.Selecting += (_, e) => e.Cancel = true;

            Assert.That(items[1].AccessibilityPerformPress(), Is.False);
            items[1].AccessibilitySelected = true;
            Assert.That(tabs.SelectedIndex, Is.Zero);
            Assert.That(changes, Is.Zero);
            Assert.That(items[0].AccessibilitySelected, Is.True);
            Assert.That(items[1].AccessibilitySelected, Is.False);
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeTabLabelsUpdateAndReorderingPreservesIdentity()
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tabs.Handle);
            var items = Items(view);
            Assert.That(items[0].AccessibilityTitle, Is.EqualTo("First & foremost"));
            tabs.TabPages[1].AccessibleName = "Named page";
            Assert.That(items[1].AccessibilityLabel, Is.EqualTo("Named page"));
            tabs.TabPages[0].Text = "&Renamed";
            Assert.That(items[0].AccessibilityTitle, Is.EqualTo("Renamed"));
            tabs.Controls.SetChildIndex(tabs.TabPages[1], 0);
            Assert.That(Items(view)[0], Is.SameAs(items[1]));
            Assert.That(Items(view)[1], Is.SameAs(items[0]));
        }
        finally { form.Close(); }
    }

    [Test]
    public void RemovedTabCannotSelectAReplacementOrReattachedPage()
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tabs.Handle);
            var detached = Items(view)[1];
            var removed = tabs.TabPages[1];
            tabs.TabPages.Remove(removed);
            tabs.TabPages.Add(new TabPage("Replacement"));
            Assert.That(Items(view)[1], Is.Not.SameAs(detached));
            detached.AccessibilitySelected = true;
            detached.AccessibilityFocused = true;
            Assert.That(detached.AccessibilityPerformPress(), Is.False);
            Assert.That(detached.AccessibilityHidden, Is.True);
            Assert.That(detached.AccessibilityParent, Is.Null);
            Assert.That(tabs.SelectedIndex, Is.Zero);
            tabs.TabPages.Add(removed);
            Assert.That(Items(view)[2], Is.Not.SameAs(detached));
            Assert.That(detached.AccessibilityPerformPress(), Is.False);
        }
        finally { form.Close(); }
    }

    [TestCase("disabled")]
    [TestCase("hidden")]
    [TestCase("page-disabled")]
    public void UnavailableTabsRejectNativeActions(string unavailable)
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        try
        {
            form.Show();
            Application.DoEvents();
            var item = Items(Runtime.GetNSObject<NSView>(tabs.Handle))[1];
            if (unavailable == "disabled") tabs.Enabled = false;
            else if (unavailable == "hidden") tabs.Visible = false;
            else tabs.TabPages[1].Enabled = false;
            Assert.That(item.AccessibilityEnabled, Is.False);
            Assert.That(item.AccessibilityPerformPress(), Is.False);
            item.AccessibilitySelected = true;
            item.AccessibilityFocused = true;
            Assert.That(tabs.SelectedIndex, Is.Zero);
        }
        finally { form.Close(); }
    }

    [Test]
    public void TabItemsDoNotDuplicateOrReplaceSelectedPagesNativeControls()
    {
        using var form = new Form();
        using var tabs = CreateTabs(form);
        using var firstEditor = new TextBox { Text = "First editor" };
        using var secondEditor = new TextBox { Text = "Second editor" };
        tabs.TabPages[0].Controls.Add(firstEditor);
        tabs.TabPages[1].Controls.Add(secondEditor);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(tabs.Handle);
            var firstElement = NSAccessibility.GetUnignoredDescendant(Runtime.GetNSObject<NSView>(firstEditor.Handle));
            var secondElement = NSAccessibility.GetUnignoredDescendant(Runtime.GetNSObject<NSView>(secondEditor.Handle));
            Assert.That(CountHandle(view, firstElement.Handle), Is.EqualTo(1));
            Assert.That(CountHandle(view, secondElement.Handle), Is.Zero);
            var items = Items(view);
            Assert.That(items.All(item => item.AccessibilityChildren == null || item.AccessibilityChildren.Length == 0), Is.True);
            Assert.That(items[1].AccessibilityPerformPress(), Is.True);
            Application.DoEvents();
            Assert.That(CountHandle(view, secondElement.Handle), Is.EqualTo(1));
            Assert.That(CountHandle(view, firstElement.Handle), Is.Zero);
        }
        finally { form.Close(); }
    }

    private static TabControl CreateTabs(Form form)
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(new TabPage("&First && foremost"));
        tabs.TabPages.Add(new TabPage("&Second"));
        form.Controls.Add(tabs);
        return tabs;
    }

    private static NSAccessibilityElement[] Items(NSView view) =>
        view.AccessibilityChildren.OfType<NSAccessibilityElement>()
            .Where(item => item.AccessibilityRole == "AXRadioButton").ToArray();

    private static int CountHandle(NSObject element, IntPtr handle)
    {
        int count = element.Handle == handle ? 1 : 0;
        NSObject[] children;
        if (element is NSView view)
        {
            if (view.AccessibilityHidden) return 0;
            children = view.AccessibilityChildren;
        }
        else if (element is NSAccessibilityElement item) children = item.AccessibilityChildren;
        else return count;
        return count + (children?.Sum(child => CountHandle(child, handle)) ?? 0);
    }
}
