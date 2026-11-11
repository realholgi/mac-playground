using System;
using System.Windows.Forms.Mac;
using AppKit;
using Foundation;

namespace System.Windows.Forms.CocoaInternal
{
	// A subscription belongs to one native handle, not the lifetime of the control.
	internal sealed class ControlAccessibilityNotifications : IDisposable
	{
		private readonly Control control;
		private readonly NSView view;
		private readonly bool managedView;
		private bool disposed;

		internal ControlAccessibilityNotifications(Control control, NSView view)
		{
			this.control = control;
			this.view = view;
			managedView = view is MonoView;
			control.TextChanged += TextChanged;
			control.EnabledChanged += StateChanged;
			control.VisibleChanged += StateChanged;
			if (managedView)
			{
				control.LocationChanged += LayoutChanged;
				control.SizeChanged += LayoutChanged;
				control.ControlAdded += ChildrenChanged;
				control.ControlRemoved += ChildrenChanged;
				// Composite bridges announce their virtual focused elements themselves.
				if (control is not DataGridView && control is not TreeView && control is not ListView && control is not TabControl)
					control.GotFocus += FocusChanged;
				if (control is CheckBox check) check.CheckStateChanged += ValueChanged;
				if (control is RadioButton radio) radio.CheckedChanged += ValueChanged;
				if (control is TrackBar slider) slider.ValueChanged += ValueChanged;
			}
			UpdateNativeMetadata();
		}

		private void Post(NSString notification)
		{
			if (!disposed && !control.IsDisposed && view.Handle != IntPtr.Zero)
				NSAccessibility.PostNotification(view, notification);
		}

		private void UpdateNativeMetadata()
		{
			if (managedView || disposed) return;
			view.AccessibilityLabel = control.AccessibilityTitle();
			view.AccessibilityHelp = control.AccessibilityObject.Description;
		}

		internal void MetadataChanged(bool description)
		{
			UpdateNativeMetadata();
			Post(description ? NSAccessibilityElement.LayoutChangedNotification : NSAccessibilityElement.TitleChangedNotification);
		}

		private void TextChanged(object sender, EventArgs e)
		{
			UpdateNativeMetadata();
			if (managedView)
			{
				Post(NSAccessibilityElement.TitleChangedNotification);
				if (view is MonoEditView) Post(NSAccessibilityElement.ValueChangedNotification);
			}
		}
		private void ValueChanged(object sender, EventArgs e) => Post(NSAccessibilityElement.ValueChangedNotification);
		private void InvalidateChildren()
		{
			for (NSView parent = view; parent != null; parent = parent.Superview)
				if (parent is MonoView managed)
				{
					managed.InvalidateAccessibilityChildren();
					return;
				}
		}
		private void StateChanged(object sender, EventArgs e)
		{
			InvalidateChildren();
			Post(NSAccessibilityElement.LayoutChangedNotification);
		}
		private void LayoutChanged(object sender, EventArgs e) => Post(NSAccessibilityElement.LayoutChangedNotification);
		private void ChildrenChanged(object sender, ControlEventArgs e)
		{
			InvalidateChildren();
			Post(NSAccessibilityElement.LayoutChangedNotification);
		}
		private void FocusChanged(object sender, EventArgs e) => Post(NSAccessibilityElement.UIElementFocusedChangedNotification);

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			control.TextChanged -= TextChanged;
			control.EnabledChanged -= StateChanged;
			control.VisibleChanged -= StateChanged;
			if (managedView)
			{
				control.LocationChanged -= LayoutChanged;
				control.SizeChanged -= LayoutChanged;
				control.ControlAdded -= ChildrenChanged;
				control.ControlRemoved -= ChildrenChanged;
				control.GotFocus -= FocusChanged;
				if (control is CheckBox check) check.CheckStateChanged -= ValueChanged;
				if (control is RadioButton radio) radio.CheckedChanged -= ValueChanged;
				if (control is TrackBar slider) slider.ValueChanged -= ValueChanged;
			}
		}
	}
}
