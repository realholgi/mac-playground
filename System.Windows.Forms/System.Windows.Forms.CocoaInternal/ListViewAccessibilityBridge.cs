using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace System.Windows.Forms.CocoaInternal
{
	internal sealed class ListViewAccessibilityBridge : IDisposable
	{
		private readonly NSView view;
		private readonly XplatUICocoa driver;
		private readonly ListView control;
		private readonly Dictionary<ListViewItem, ItemElement> items = new Dictionary<ListViewItem, ItemElement>();
		private readonly Dictionary<int, ItemElement> virtualItems = new Dictionary<int, ItemElement>();
		private readonly Dictionary<ColumnHeader, HeaderElement> headers = new Dictionary<ColumnHeader, HeaderElement>();
		private bool disposed;
		private int generation;
		private bool virtualMode;
		private NSObject[] cachedChildren, cachedRows, cachedHeaders, cachedSelection;
		private int columnVersion;
		private View cachedView;
		private ColumnHeaderStyle cachedHeaderStyle;
		private bool Live => !disposed && !control.IsDisposed;
		private bool CanAct => Live && control.Enabled && control.Visible;
		private bool Details => control.View == View.Details;

		internal ListViewAccessibilityBridge(NSView view, XplatUICocoa driver, ListView control)
		{
			this.view = view;
			this.driver = driver;
			this.control = control;
			virtualMode = control.VirtualMode;
			cachedView = control.View;
			cachedHeaderStyle = control.HeaderStyle;
			control.Items.Changed += ItemsChanged;
			control.Columns.UIACollectionChanged += ColumnsChanged;
			control.AccessibilityColumnOrderChanged += ColumnOrderChanged;
			control.SelectedIndexChanged += SelectionChanged;
			control.ItemChecked += CheckedChanged;
			control.UIAFocusedItemChanged += FocusChanged;
			control.GotFocus += FocusChanged;
			control.LostFocus += FocusChanged;
			control.UIAViewChanged += StructureChanged;
			control.UIACheckBoxesChanged += StructureChanged;
			control.AccessibilityVirtualItemsChanged += VirtualChanged;
			control.Disposed += ControlDisposed;
			control.UIAHScrollBar.ValueChanged += StructureChanged;
			control.UIAVScrollBar.ValueChanged += StructureChanged;
			control.Invalidated += LayoutInvalidated;
			control.SizeChanged += StructureChanged;
		}

		// Virtual lists expose the visible window; parameterized table lookup addresses every index.
		internal NSObject[] Children
		{
			get
			{
				if (!Live) return Array.Empty<NSObject>();
				CheckPresentation();
				if (cachedChildren != null) return cachedChildren;
				var rows = Rows;
				var columns = ColumnHeaders;
				if (columns.Length == 0) return cachedChildren = rows;
				var children = new NSObject[columns.Length + rows.Length];
				Array.Copy(columns, children, columns.Length);
				Array.Copy(rows, 0, children, columns.Length, rows.Length);
				return cachedChildren = children;
			}
		}
		internal NSObject[] Rows
		{
			get
			{
				if (!Live || !control.IsHandleCreated) return Array.Empty<NSObject>();
				if (cachedRows != null) return cachedRows;
				int count = control.Items.Count;
				int first = control.VirtualMode ? Math.Min(count, Math.Max(0, control.FirstVisibleIndex)) : 0;
				int end = control.VirtualMode ? Math.Min(count, Math.Max(first, control.LastVisibleIndex + 1)) : count;
				var rows = new NSObject[end - first];
				for (int i = first; i < end; ++i) rows[i - first] = ItemAt(i);
				return cachedRows = rows;
			}
		}
		internal nint RowCount => Live ? control.Items.Count : 0;
		internal nint ColumnCount => Live && Details ? control.Columns.Count : 0;
		internal NSObject[] ColumnHeaders
		{
			get
			{
				if (!Live || !Details || control.HeaderStyle == ColumnHeaderStyle.None) return Array.Empty<NSObject>();
				if (cachedHeaders != null) return cachedHeaders;
				var result = new NSObject[control.Columns.Count];
				for (int i = 0; i < result.Length; ++i)
				{
					ColumnHeader column = control.GetReorderedColumn(i);
					if (!headers.TryGetValue(column, out var header)) headers.Add(column, header = new HeaderElement(this, column));
					result[i] = header;
				}
				return cachedHeaders = result;
			}
		}
		internal NSObject[] SelectedRows
		{
			get
			{
				if (!Live) return Array.Empty<NSObject>();
				if (cachedSelection != null) return cachedSelection;
				var result = new NSObject[control.SelectedIndices.Count];
				for (int i = 0; i < result.Length; ++i) result[i] = ItemAt(control.SelectedIndices[i]);
				return cachedSelection = result;
			}
		}
		internal NSObject FocusedElement => Live && control.ContainsFocus && control.focused_item_index >= 0 && control.focused_item_index < control.Items.Count
			? ItemAt(control.focused_item_index) : null;
		internal NSObject CellAt(nint column, nint row) => Live && Details && row >= 0 && row < control.Items.Count && column >= 0 && column < control.Columns.Count
			? ItemAt((int)row).Cell(control.GetReorderedColumn((int)column)) : null;

		private ItemElement ItemAt(int index)
		{
			if (control.VirtualMode)
			{
				if (!virtualItems.TryGetValue(index, out var element)) virtualItems.Add(index, element = new ItemElement(this, index, generation));
				return element;
			}
			ListViewItem item = control.Items[index];
			if (!items.TryGetValue(item, out var result)) items.Add(item, result = new ItemElement(this, item));
			return result;
		}
		internal NSObject HitTest(CGPoint nativeScreenPoint)
		{
			if (!Live) return null;
			Point screen = driver.NativeToMonoScreen(nativeScreenPoint);
			if (!control.RectangleToScreen(control.ClientRectangle).Contains(screen)) return null;
			foreach (HeaderElement header in ColumnHeaders)
				if (header.AccessibilityFrame.Contains(nativeScreenPoint)) return header;
			Point client = control.item_control.PointToClient(screen);
			if (client.X < 0 || client.Y < 0) return view;
			ListViewItem item = control.GetItemAt(client.X, client.Y);
			if (item == null) return view;
			var row = ItemAt(item.Index);
			if (Details)
				foreach (ColumnHeader column in control.Columns)
					if (row.Cell(column).AccessibilityFrame.Contains(nativeScreenPoint)) return row.Cell(column);
			return row;
		}
		private CGRect Frame(Rectangle bounds) => driver.MonoToNativeScreen(control.item_control.RectangleToScreen(bounds));
		private void ItemsChanged()
		{
			List<ListViewItem> removed = null;
			foreach (var pair in items)
				if (pair.Key.ListView != control || !control.Items.Contains(pair.Key))
				{
					pair.Value.Detach();
					(removed ??= new List<ListViewItem>()).Add(pair.Key);
				}
			if (removed != null) foreach (var item in removed) items.Remove(item);
			StructureChanged(control, EventArgs.Empty);
		}
		private void ColumnsChanged(object sender, CollectionChangeEventArgs e)
		{
			++columnVersion;
			List<ColumnHeader> removed = null;
			foreach (var pair in headers)
				if (pair.Key.ListView != control)
				{
					pair.Value.Detach();
					(removed ??= new List<ColumnHeader>()).Add(pair.Key);
				}
			if (removed != null) foreach (var column in removed) headers.Remove(column);
			foreach (var item in items.Values) item.CheckColumns();
			foreach (var item in virtualItems.Values) item.CheckColumns();
			StructureChanged(sender, e);
		}
		private void VirtualChanged(object sender, EventArgs e)
		{
			bool modeChanged = virtualMode != control.VirtualMode;
			virtualMode = control.VirtualMode;
			if (modeChanged) ++generation;
			List<int> removed = null;
			foreach (var pair in virtualItems)
				if (modeChanged || pair.Key >= control.VirtualListSize)
				{
					pair.Value.Detach();
					(removed ??= new List<int>()).Add(pair.Key);
				}
			if (removed != null) foreach (int index in removed) virtualItems.Remove(index);
			StructureChanged(sender, e);
		}
		private void ColumnOrderChanged(object sender, EventArgs e)
		{
			++columnVersion;
			StructureChanged(sender, e);
		}
		private void LayoutInvalidated(object sender, InvalidateEventArgs e) => StructureChanged(sender, e);
		private void CheckPresentation()
		{
			if (cachedView == control.View && cachedHeaderStyle == control.HeaderStyle) return;
			cachedView = control.View;
			cachedHeaderStyle = control.HeaderStyle;
			++columnVersion;
			cachedChildren = cachedRows = cachedHeaders = null;
		}
		private void StructureChanged(object sender, EventArgs e)
		{
			cachedChildren = cachedRows = cachedHeaders = cachedSelection = null;
			if (Live) NSAccessibility.PostNotification(view, NSAccessibilityElement.LayoutChangedNotification);
		}
		private void SelectionChanged(object sender, EventArgs e)
		{
			cachedSelection = null;
			if (Live) NSAccessibility.PostNotification(view, NSAccessibilityElement.SelectedChildrenChangedNotification);
		}
		private void FocusChanged(object sender, EventArgs e)
		{
			if (Live) NSAccessibility.PostNotification(FocusedElement ?? view, NSAccessibilityElement.UIElementFocusedChangedNotification);
		}
		private void CheckedChanged(object sender, ItemCheckedEventArgs e)
		{
			if (!Live) return;
			ItemElement element = null;
			if (control.VirtualMode) virtualItems.TryGetValue(e.Item.Index, out element);
			else items.TryGetValue(e.Item, out element);
			if (element != null) NSAccessibility.PostNotification(element, NSAccessibilityElement.ValueChangedNotification);
		}
		private void ControlDisposed(object sender, EventArgs e) => Dispose();
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			control.Items.Changed -= ItemsChanged;
			control.Columns.UIACollectionChanged -= ColumnsChanged;
			control.AccessibilityColumnOrderChanged -= ColumnOrderChanged;
			control.SelectedIndexChanged -= SelectionChanged;
			control.ItemChecked -= CheckedChanged;
			control.UIAFocusedItemChanged -= FocusChanged;
			control.GotFocus -= FocusChanged;
			control.LostFocus -= FocusChanged;
			control.UIAViewChanged -= StructureChanged;
			control.UIACheckBoxesChanged -= StructureChanged;
			control.AccessibilityVirtualItemsChanged -= VirtualChanged;
			control.Disposed -= ControlDisposed;
			control.UIAHScrollBar.ValueChanged -= StructureChanged;
			control.UIAVScrollBar.ValueChanged -= StructureChanged;
			control.Invalidated -= LayoutInvalidated;
			control.SizeChanged -= StructureChanged;
			foreach (var item in items.Values) item.Detach();
			foreach (var item in virtualItems.Values) item.Detach();
			foreach (var header in headers.Values) header.Detach();
			items.Clear(); virtualItems.Clear(); headers.Clear();
			cachedChildren = cachedRows = cachedHeaders = cachedSelection = null;
		}

		private sealed class ItemElement : NSAccessibilityElement
		{
			private readonly ListViewAccessibilityBridge bridge;
			private readonly ListViewItem item;
			private readonly int virtualIndex = -1;
			private readonly int generation;
			private bool detached;
			private readonly Dictionary<ColumnHeader, CellElement> cells = new Dictionary<ColumnHeader, CellElement>();
			private NSObject[] cachedChildren;
			private int childrenVersion = -1;
			internal ItemElement(ListViewAccessibilityBridge bridge, ListViewItem item)
			{
				this.bridge = bridge; this.item = item;
				item.UIATextChanged += TextChanged;
				item.UIASubItemTextChanged += SubItemChanged;
			}
			internal ItemElement(ListViewAccessibilityBridge bridge, int index, int generation)
			{ this.bridge = bridge; virtualIndex = index; this.generation = generation; }
			internal bool IsLive
			{
				get
				{
					if (detached || !bridge.Live) return false;
					if (virtualIndex >= 0)
						return bridge.control.VirtualMode && generation == bridge.generation && virtualIndex < bridge.control.VirtualListSize;
					// Items.Changed fires during sorting, before display-index mappings are rebuilt.
					// Mutation-time membership checks detach removals; a surviving item's old
					// display index must never be mistaken for replacement identity.
					return !bridge.control.VirtualMode && item.ListView == bridge.control;
				}
			}
			private bool CanAct => IsLive && bridge.CanAct;
			internal int Index => !IsLive ? -1 : virtualIndex >= 0 ? virtualIndex : item.Index;
			internal ListViewItem Item => !IsLive ? null : virtualIndex >= 0 ? bridge.control.Items[virtualIndex] : item;
			internal CellElement Cell(ColumnHeader column)
			{
				if (!cells.TryGetValue(column, out var cell)) cells.Add(column, cell = new CellElement(bridge, this, column));
				return cell;
			}
			internal void CheckColumns()
			{
				List<ColumnHeader> removed = null;
				foreach (var pair in cells)
					if (pair.Key.ListView != bridge.control) { pair.Value.Detach(); (removed ??= new List<ColumnHeader>()).Add(pair.Key); }
				if (removed != null) foreach (var column in removed) cells.Remove(column);
			}
			internal void Detach()
			{
				if (detached) return;
				detached = true;
				if (item != null) { item.UIATextChanged -= TextChanged; item.UIASubItemTextChanged -= SubItemChanged; }
				foreach (var cell in cells.Values) cell.Detach();
				cells.Clear();
				cachedChildren = null;
				NSAccessibility.PostNotification(this, UIElementDestroyedNotification);
			}
			private void TextChanged(object sender, EventArgs e)
			{ if (IsLive) NSAccessibility.PostNotification(this, ValueChangedNotification); }
			private void SubItemChanged(object sender, LabelEditEventArgs e)
			{
				foreach (var pair in cells)
					if (pair.Key.Index == e.Item) NSAccessibility.PostNotification(pair.Value, ValueChangedNotification);
			}
			public override bool AccessibilityElement => IsLive;
			public override bool AccessibilityHidden => !IsLive;
			public override bool AccessibilityEnabled => CanAct;
			public override string AccessibilityRole => !IsLive ? null : bridge.control.CheckBoxes && !bridge.Details ? "AXCheckBox" : "AXRow";
			public override string AccessibilityLabel => Item?.Text ?? string.Empty;
			public override string AccessibilityTitle => AccessibilityLabel;
			public override NSObject AccessibilityParent => IsLive ? bridge.view : null;
			public override NSObject AccessibilityWindow => IsLive ? bridge.view.Window : null;
			public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
			public override CGRect AccessibilityFrame => IsLive ? bridge.Frame(Item.Bounds) : CGRect.Empty;
			public override nint AccessibilityIndex => Index;
			public override NSRange AccessibilityRowIndexRange => new NSRange(Index, IsLive ? 1 : 0);
			public override NSObject AccessibilityValue
			{
				get => !IsLive ? null : bridge.control.CheckBoxes ? (NSObject)new NSNumber(Item.Checked) : new NSString(Item.Text);
				set { if (CanAct && bridge.control.CheckBoxes && value is NSNumber number && (number.DoubleValue == 0 || number.DoubleValue == 1)) Item.Checked = number.BoolValue; }
			}
			public override bool AccessibilitySelected
			{
				get => IsLive && bridge.control.SelectedIndices.Contains(Index);
				set { if (CanAct) Item.Selected = value; }
			}
			public override bool AccessibilityFocused
			{
				get => IsLive && bridge.control.Focused && bridge.control.focused_item_index == Index;
				set { if (value && CanAct) { Item.Focused = true; bridge.control.Focus(); } }
			}
			public override bool AccessibilityPerformPress()
			{
				if (!CanAct) return false;
				if (bridge.control.CheckBoxes) { var target = Item; target.Checked = !target.Checked; }
				else { Item.Selected = true; Item.Focused = true; }
				return true;
			}
			public override bool IsAccessibilitySelectorAllowed(Selector selector) => selector.Name switch
			{
				"accessibilityPerformPress" or "setAccessibilitySelected:" or "setAccessibilityFocused:" => CanAct,
				"setAccessibilityValue:" => CanAct && bridge.control.CheckBoxes,
				_ => base.IsAccessibilitySelectorAllowed(selector)
			};
			[Export("accessibilityIsAttributeSettable:")]
			public bool IsAttributeSettable(NSString attribute) => attribute?.ToString() switch
			{ "AXSelected" or "AXFocused" => CanAct, "AXValue" => CanAct && bridge.control.CheckBoxes, _ => false };
			public override NSObject[] AccessibilityChildren
			{
				get
				{
					if (!IsLive || !bridge.Details) return Array.Empty<NSObject>();
					if (cachedChildren != null && childrenVersion == bridge.columnVersion) return cachedChildren;
					var result = new NSObject[bridge.control.Columns.Count];
					for (int i = 0; i < result.Length; ++i) result[i] = Cell(bridge.control.GetReorderedColumn(i));
					childrenVersion = bridge.columnVersion;
					return cachedChildren = result;
				}
			}
		}

		private sealed class CellElement(ListViewAccessibilityBridge bridge, ItemElement row, ColumnHeader column) : NSAccessibilityElement
		{
			private bool detached;
			private bool Live => !detached && row.IsLive && bridge.Details && column.ListView == bridge.control;
			internal void Detach() { detached = true; NSAccessibility.PostNotification(this, UIElementDestroyedNotification); }
			public override bool AccessibilityElement => Live;
			public override bool AccessibilityHidden => !Live;
			public override bool AccessibilityEnabled => Live && bridge.CanAct;
			public override string AccessibilityRole => Live ? "AXCell" : null;
			public override string AccessibilityLabel => Live ? column.Text : string.Empty;
			public override NSObject AccessibilityValue
			{
				get { var item = Live ? row.Item : null; return item == null ? null : new NSString(column.Index < item.SubItems.Count ? item.SubItems[column.Index].Text : string.Empty); }
				set { }
			}
			public override NSObject AccessibilityParent => Live ? row : null;
			public override NSObject AccessibilityWindow => Live ? bridge.view.Window : null;
			public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
			public override CGRect AccessibilityFrame
			{
				get
				{
					if (!Live) return CGRect.Empty;
					Rectangle bounds = row.Item.Bounds;
					bounds.X = column.Rect.X - bridge.control.h_marker;
					bounds.Width = Math.Max(0, column.Width);
					return bridge.Frame(bounds);
				}
			}
			public override NSRange AccessibilityRowIndexRange => new NSRange(Live ? row.Index : -1, Live ? 1 : 0);
			public override NSRange AccessibilityColumnIndexRange => new NSRange(Live ? bridge.control.GetReorderedColumnIndex(column) : -1, Live ? 1 : 0);
			public override bool AccessibilitySelected { get => Live && row.AccessibilitySelected; set { if (Live) row.AccessibilitySelected = value; } }
			public override bool AccessibilityFocused { get => Live && row.AccessibilityFocused; set { if (Live) row.AccessibilityFocused = value; } }
		}

		private sealed class HeaderElement : NSAccessibilityElement
		{
			private readonly ListViewAccessibilityBridge bridge;
			private readonly ColumnHeader column;
			private bool detached;
			internal HeaderElement(ListViewAccessibilityBridge bridge, ColumnHeader column)
			{
				this.bridge = bridge; this.column = column;
				column.UIATextChanged += TextChanged;
			}
			private bool Live => !detached && bridge.Live && bridge.Details && bridge.control.HeaderStyle != ColumnHeaderStyle.None && column.ListView == bridge.control;
			private void TextChanged(object sender, EventArgs e)
			{ if (Live) NSAccessibility.PostNotification(this, ValueChangedNotification); }
			internal void Detach()
			{
				if (detached) return;
				detached = true;
				column.UIATextChanged -= TextChanged;
				NSAccessibility.PostNotification(this, UIElementDestroyedNotification);
			}
			public override bool AccessibilityElement => Live;
			public override bool AccessibilityHidden => !Live;
			public override bool AccessibilityEnabled => Live && bridge.CanAct;
			public override string AccessibilityRole => Live ? "AXButton" : null;
			public override string AccessibilityLabel => Live ? column.Text : string.Empty;
			public override string AccessibilityTitle => AccessibilityLabel;
			public override NSObject AccessibilityValue { get => Live ? new NSString(column.Text) : null; set { } }
			public override NSObject AccessibilityParent => Live ? bridge.view : null;
			public override NSObject AccessibilityWindow => Live ? bridge.view.Window : null;
			public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
			public override NSRange AccessibilityColumnIndexRange => new NSRange(Live ? bridge.control.GetReorderedColumnIndex(column) : -1, Live ? 1 : 0);
			public override CGRect AccessibilityFrame
			{
				get
				{
					if (!Live) return CGRect.Empty;
					Rectangle bounds = column.Rect; bounds.X -= bridge.control.h_marker;
					return bridge.driver.MonoToNativeScreen(bridge.control.header_control.RectangleToScreen(bounds));
				}
			}
			public override bool AccessibilityPerformPress()
			{
				if (!Live || !bridge.CanAct || bridge.control.HeaderStyle != ColumnHeaderStyle.Clickable) return false;
				bridge.control.OnColumnClick(new ColumnClickEventArgs(column.Index));
				return true;
			}
		}
	}
}
