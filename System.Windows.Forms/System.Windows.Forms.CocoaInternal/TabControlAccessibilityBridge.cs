using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.Mac;
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace System.Windows.Forms.CocoaInternal
{
    // Only the tab strip is virtual. The selected page and its editors remain native children.
    internal sealed class TabControlAccessibilityBridge : IDisposable
    {
        private sealed class TabElement : NSAccessibilityElement
        {
            private TabControlAccessibilityBridge bridge;
            internal readonly TabPage Page;

            internal TabElement(TabControlAccessibilityBridge bridge, TabPage page)
            {
                this.bridge = bridge;
                Page = page;
            }

            internal bool IsLive => bridge != null && bridge.IsLive && !Page.IsDisposed &&
                ReferenceEquals(Page.Parent, bridge.control) && bridge.control.TabPages.Contains(Page);
            private bool CanAct => IsLive && bridge.control.Visible && bridge.control.Enabled && Page.Enabled;
            private bool IsSelected => IsLive && ReferenceEquals(bridge.control.SelectedTab, Page);
            internal void Detach() => bridge = null;

            private bool Select(bool focus)
            {
                if (!CanAct) return false;
                // Re-resolve by page identity: an index captured before a reorder/removal is not safe.
                bridge.control.SelectedTab = Page;
                if (!IsLive || !IsSelected) return false;
                if (focus) bridge.control.Focus();
                return true;
            }

            public override bool AccessibilityElement => IsLive;
            public override string AccessibilityRole => IsLive ? "AXRadioButton" : null;
            public override string AccessibilityTitle => !IsLive ? string.Empty :
                !string.IsNullOrEmpty(Page.AccessibleName) ? Page.AccessibleName : (Page.Text ?? string.Empty).RemoveMnemonic();
            public override string AccessibilityLabel => AccessibilityTitle;
            public override string AccessibilityHelp => IsLive ? Page.AccessibleDescription : null;
            public override NSObject AccessibilityParent => IsLive ? bridge.view : null;
            public override NSObject AccessibilityWindow => IsLive ? bridge.view.Window : null;
            public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
            public override NSObject[] AccessibilityChildren => Array.Empty<NSObject>();
            public override bool AccessibilityHidden => !IsLive || !bridge.control.Visible;
            public override bool AccessibilityEnabled => CanAct;
            public override CGRect AccessibilityFrame => IsLive && bridge.control.Visible
                ? bridge.driver.MonoToNativeScreen(bridge.control.RectangleToScreen(bridge.Bounds(Page))) : CGRect.Empty;
            public override nint AccessibilityIndex => IsLive ? bridge.control.TabPages.IndexOf(Page) : -1;
            public override bool AccessibilitySelected
            {
                get => IsSelected;
                set { if (value) Select(false); }
            }
            public override bool AccessibilityFocused
            {
                get => IsSelected && bridge.control.Focused;
                set { if (value) Select(true); }
            }
            public override NSObject AccessibilityValue
            {
                get => IsLive ? new NSNumber(IsSelected ? 1 : 0) : null;
                set { if (value is NSNumber number && number.Int32Value == 1) Select(false); }
            }
            public override bool AccessibilityPerformPress() => Select(false);
            public override bool IsAccessibilitySelectorAllowed(Selector selector)
            {
                switch (selector.Name)
                {
                    case "accessibilityPerformPress":
                    case "setAccessibilitySelected:":
                    case "setAccessibilityFocused:":
                    case "setAccessibilityValue:": return CanAct;
                    default: return base.IsAccessibilitySelectorAllowed(selector);
                }
            }
            [Export("accessibilityIsAttributeSettable:")]
            public bool AccessibilityIsAttributeSettable(NSString attribute) => CanAct &&
                (attribute?.ToString() is "AXSelected" or "AXFocused" or "AXValue");
            [Export("accessibilityIsFocusedAttributeSettable")]
            public bool AccessibilityIsFocusedAttributeSettable() => CanAct;
            [Export("accessibilityIsValueAttributeSettable")]
            public bool AccessibilityIsValueAttributeSettable() => CanAct;
        }

        private readonly NSView view;
        private readonly XplatUICocoa driver;
        private readonly TabControl control;
        private readonly Dictionary<TabPage, TabElement> elements = new Dictionary<TabPage, TabElement>();
        private NSObject[] children;
        private NSObject[] selectedChildren;
        private bool disposed;
        private bool IsLive => !disposed && !control.IsDisposed;

        internal TabControlAccessibilityBridge(NSView view, XplatUICocoa driver, TabControl control)
        {
            this.view = view;
            this.driver = driver;
            this.control = control;
            control.ControlAdded += PageAdded;
            control.ControlRemoved += PageRemoved;
            control.Layout += LayoutChanged;
            control.SelectedIndexChanged += SelectionChanged;
            control.GotFocus += FocusChanged;
            control.LostFocus += FocusChanged;
            control.VisibleChanged += StateChanged;
            control.EnabledChanged += StateChanged;
            control.Disposed += ControlDisposed;
            foreach (TabPage page in control.TabPages) Add(page);
        }

        internal NSObject[] Children
        {
            get
            {
                if (!IsLive) return Array.Empty<NSObject>();
                if (children == null)
                {
                    children = new NSObject[control.TabCount];
                    for (int i = 0; i < children.Length; i++) children[i] = Add(control.TabPages[i]);
                }
                return children;
            }
        }
        internal NSObject[] SelectedChildren => !IsLive ? Array.Empty<NSObject>() :
            selectedChildren ??= control.SelectedTab is TabPage page
                ? new NSObject[] { Add(page) } : Array.Empty<NSObject>();
        internal NSObject SelectedElement => IsLive && control.SelectedTab is TabPage page ? Add(page) : null;
        internal NSObject FocusedElement => IsLive && control.Focused ? SelectedElement : null;

        internal NSObject HitTest(CGPoint nativeScreenPoint)
        {
            if (!IsLive || !control.Visible) return null;
            Point point = control.PointToClient(driver.NativeToMonoScreen(nativeScreenPoint));
            // Return null in page content so the parent uses AppKit's actual native-child hit test.
            foreach (TabPage page in control.TabPages)
                if (Bounds(page).Contains(point)) return Add(page);
            return null;
        }

        private Rectangle Bounds(TabPage page)
        {
            int index = control.TabPages.IndexOf(page);
            return index < 0 ? Rectangle.Empty : Rectangle.Intersect(control.GetTabRect(index), control.ClientRectangle);
        }
        private TabElement Add(TabPage page)
        {
            if (elements.TryGetValue(page, out var element)) return element;
            element = new TabElement(this, page);
            elements.Add(page, element);
            page.TextChanged += TitleChanged;
            page.AccessibilityMetadataChanged += TitleChanged;
            page.EnabledChanged += StateChanged;
            page.Disposed += PageDisposed;
            return element;
        }
        private void Remove(TabPage page)
        {
            if (!elements.TryGetValue(page, out var element)) return;
            page.TextChanged -= TitleChanged;
            page.AccessibilityMetadataChanged -= TitleChanged;
            page.EnabledChanged -= StateChanged;
            page.Disposed -= PageDisposed;
            elements.Remove(page);
            element.Detach();
            NSAccessibility.PostNotification(element, NSAccessibilityElement.UIElementDestroyedNotification);
        }
        private void PageAdded(object sender, ControlEventArgs e)
        {
            if (e.Control is TabPage page) Add(page);
            StructureChanged();
        }
        private void PageRemoved(object sender, ControlEventArgs e)
        {
            if (e.Control is TabPage page) Remove(page);
            StructureChanged();
        }
        private void PageDisposed(object sender, EventArgs e)
        {
            Remove((TabPage)sender);
            StructureChanged();
        }
        private void StructureChanged()
        {
            children = null;
            selectedChildren = null;
            if (IsLive) NSAccessibility.PostNotification(view, NSAccessibilityElement.LayoutChangedNotification);
        }
        private void LayoutChanged(object sender, LayoutEventArgs e) => StructureChanged();
        private void StateChanged(object sender, EventArgs e) => StructureChanged();
        private void TitleChanged(object sender, EventArgs e)
        {
            if (IsLive && sender is TabPage page && elements.TryGetValue(page, out var element))
            {
                NSAccessibility.PostNotification(element, NSAccessibilityElement.TitleChangedNotification);
                NSAccessibility.PostNotification(view, NSAccessibilityElement.LayoutChangedNotification);
            }
        }
        private void SelectionChanged(object sender, EventArgs e)
        {
            if (!IsLive) return;
            selectedChildren = null;
            NSAccessibility.PostNotification(view, NSAccessibilityElement.SelectedChildrenChangedNotification);
            foreach (var element in elements.Values)
                NSAccessibility.PostNotification(element, NSAccessibilityElement.ValueChangedNotification);
            NSAccessibility.PostNotification(view, NSAccessibilityElement.LayoutChangedNotification);
            FocusChanged(sender, e);
        }
        private void FocusChanged(object sender, EventArgs e)
        {
            if (IsLive) NSAccessibility.PostNotification(FocusedElement ?? view, NSAccessibilityElement.UIElementFocusedChangedNotification);
        }
        private void ControlDisposed(object sender, EventArgs e) => Dispose();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            control.ControlAdded -= PageAdded;
            control.ControlRemoved -= PageRemoved;
            control.Layout -= LayoutChanged;
            control.SelectedIndexChanged -= SelectionChanged;
            control.GotFocus -= FocusChanged;
            control.LostFocus -= FocusChanged;
            control.VisibleChanged -= StateChanged;
            control.EnabledChanged -= StateChanged;
            control.Disposed -= ControlDisposed;
            foreach (var pair in elements)
            {
                pair.Key.TextChanged -= TitleChanged;
                pair.Key.AccessibilityMetadataChanged -= TitleChanged;
                pair.Key.EnabledChanged -= StateChanged;
                pair.Key.Disposed -= PageDisposed;
                pair.Value.Detach();
            }
            elements.Clear();
            children = null;
            selectedChildren = null;
        }
    }
}
