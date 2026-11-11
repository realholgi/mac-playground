using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms.Mac;
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace System.Windows.Forms.CocoaInternal
{
	// This is an adapter over the managed accessibility tree, not a second grid model.
	internal sealed class AccessibleObjectElement : NSAccessibilityElement
	{
		internal readonly AccessibleObject AccessibleObject;
		private GridAccessibilityBridge bridge;
		private readonly IDataGridViewAccessibleNode node;
		private readonly DataGridViewColumn column;
		private NSObject[] children;
		private int childrenVersion = -1;

		internal AccessibleObjectElement(GridAccessibilityBridge bridge, AccessibleObject accessibleObject)
		{
			this.bridge = bridge;
			AccessibleObject = accessibleObject;
			node = (IDataGridViewAccessibleNode)accessibleObject;
			column = node.Cell?.OwningColumn;
		}

		internal bool IsLive
		{
			get
			{
				if (bridge == null || !bridge.IsLive || node.Grid != bridge.Grid)
					return false;
				if (node.Row is DataGridViewRow row &&
					(row.DataGridView != bridge.Grid || row.Index < 0 || row.Index >= bridge.Grid.Rows.Count ||
					 !ReferenceEquals(bridge.Grid.Rows[row.Index], row) || !row.Visible))
					return false;
				if (node.Cell is DataGridViewCell cell)
				{
					if (cell.DataGridView != bridge.Grid)
						return false;
					if (column != null && (column.DataGridView != bridge.Grid || !column.Visible ||
						!ReferenceEquals(cell.OwningColumn, column)))
						return false;
					if (cell is DataGridViewColumnHeaderCell)
						return bridge.Grid.ColumnHeadersVisible && column != null && ReferenceEquals(column.HeaderCell, cell);
					if (cell is DataGridViewRowHeaderCell)
						return bridge.Grid.RowHeadersVisible && node.Row != null && ReferenceEquals(node.Row.HeaderCell, cell);
					return node.Row != null && ReferenceEquals(cell.OwningRow, node.Row);
				}
				return node.Row != null || bridge.Grid.ColumnHeadersVisible;
			}
		}

		internal void Detach()
		{
			bridge = null;
			children = null;
		}

		private AccessibleStates State => IsLive ? AccessibleObject.State : AccessibleStates.Unavailable | AccessibleStates.Offscreen;
		private bool CanAct => IsLive && bridge.Grid.Enabled && bridge.Grid.Visible && (State & AccessibleStates.Unavailable) == 0;
		private bool CanEdit => CanAct && node.Cell != null && node.Cell is not DataGridViewHeaderCell &&
			!bridge.Grid.ReadOnly && !node.Cell.ReadOnly && (State & AccessibleStates.ReadOnly) == 0 &&
			!string.IsNullOrEmpty(AccessibleObject.DefaultAction);

		public override bool AccessibilityElement => IsLive;
		public override string AccessibilityRole => IsLive ? AccessibleObject.Role.ToNSAccessibilityRole() : null;
		public override string AccessibilityTitle => IsLive ? AccessibleObject.Name : string.Empty;
		public override string AccessibilityLabel => AccessibilityTitle;
		public override string AccessibilityHelp => IsLive ? AccessibleObject.Description : null;
		public override bool AccessibilityEnabled => CanAct;
		public override bool AccessibilityHidden => !IsLive;
		public override NSObject AccessibilityParent => IsLive ? bridge.GetElement(AccessibleObject.Parent) : null;
		public override NSObject AccessibilityWindow => bridge?.View.Window;
		public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
		public override CGRect AccessibilityFrame => IsLive ? bridge.Driver.MonoToNativeScreen(AccessibleObject.Bounds) : CGRect.Empty;
		public override bool AccessibilityFocused
		{
			get => (State & AccessibleStates.Focused) != 0;
			set { if (value && CanAct) AccessibleObject.Select(AccessibleSelection.TakeFocus | AccessibleSelection.TakeSelection); }
		}
		public override bool AccessibilitySelected
		{
			get => (State & AccessibleStates.Selected) != 0;
			set { if (CanAct) AccessibleObject.Select(value ? AccessibleSelection.AddSelection : AccessibleSelection.RemoveSelection); }
		}
		public override NSObject AccessibilityValue
		{
			get => IsLive && node.Cell != null ? new NSString(AccessibleObject.Value ?? string.Empty) : null;
			set { if (CanEdit && value is NSString text) AccessibleObject.Value = text.ToString(); }
		}
		[Export("accessibilityIsValueAttributeSettable")]
		public bool AccessibilityIsValueAttributeSettable() => CanEdit;
		[Export("accessibilityIsFocusedAttributeSettable")]
		public bool AccessibilityIsFocusedAttributeSettable() => CanAct;
		[Export("accessibilityIsAttributeSettable:")]
		public bool AccessibilityIsAttributeSettable(NSString attribute)
		{
			switch (attribute?.ToString())
			{
				case "AXValue": return CanEdit;
				case "AXFocused":
				case "AXSelected": return CanAct;
				default: return false;
			}
		}
		public override bool IsAccessibilitySelectorAllowed(Selector selector)
		{
			switch (selector.Name)
			{
				case "accessibilityPerformPress": return CanEdit;
				case "setAccessibilityValue:": return CanEdit;
				case "setAccessibilityFocused:": return CanAct;
			}
			return base.IsAccessibilitySelectorAllowed(selector);
		}
		public override bool AccessibilityPerformPress()
		{
			if (!CanEdit) return false;
			AccessibleObject.DoDefaultAction();
			return true;
		}
		public override NSObject[] AccessibilityChildren
		{
			get
			{
				if (!IsLive) return Array.Empty<NSObject>();
				bridge.CheckHeaders();
				if (!IsLive) return Array.Empty<NSObject>();
				if (childrenVersion != bridge.ChildrenVersion)
				{
					children = bridge.GetChildren(AccessibleObject);
					childrenVersion = bridge.ChildrenVersion;
				}
				return children;
			}
		}
		public override nint AccessibilityIndex => IsLive ? bridge.RowIndex(node.Row) : -1;
		public override NSRange AccessibilityRowIndexRange => new NSRange(AccessibilityIndex, IsLive && node.Row != null ? 1 : 0);
		public override NSRange AccessibilityColumnIndexRange => new NSRange(IsLive ? bridge.ColumnIndex(column) : -1, IsLive && column != null ? 1 : 0);
		[Export("accessibilityHitTest:")]
		public NSObject HitTest(CGPoint point) => IsLive ? bridge.HitTest(point) : null;
		[Export("accessibilityFocusedUIElement")]
		public NSObject FocusedElement() => IsLive ? bridge.FocusedElement : null;
	}

	internal sealed class GridAccessibilityBridge : IDisposable
	{
		private sealed class IdentityComparer : IEqualityComparer<AccessibleObject>
		{
			public bool Equals(AccessibleObject x, AccessibleObject y) => ReferenceEquals(x, y);
			public int GetHashCode(AccessibleObject value) => RuntimeHelpers.GetHashCode(value);
		}
		internal readonly DataGridView Grid;
		internal readonly MonoView View;
		internal readonly XplatUICocoa Driver;
		private readonly Dictionary<AccessibleObject, AccessibleObjectElement> elements = new Dictionary<AccessibleObject, AccessibleObjectElement>(new IdentityComparer());
		private NSObject[] rows;
		private NSObject[] combinedChildren;
		private NSObject[] nativeChildren;
		private NSObject[] selectedCells;
		private NSObject[] managedChildren;
		private NSObject[] selectedRows;
		private bool columnHeadersVisible;
		private bool rowHeadersVisible;
		private bool disposed;
		internal int ChildrenVersion { get; private set; }
		internal bool IsLive => !disposed && !Grid.IsDisposed;

		internal GridAccessibilityBridge(MonoView view, XplatUICocoa driver, DataGridView grid)
		{
			View = view;
			Driver = driver;
			Grid = grid;
			columnHeadersVisible = grid.ColumnHeadersVisible;
			rowHeadersVisible = grid.RowHeadersVisible;
			grid.RowsAdded += RowsAdded;
			grid.RowsRemoved += RowsRemoved;
			grid.ColumnAdded += ColumnChanged;
			grid.ColumnRemoved += ColumnChanged;
			grid.ColumnDisplayIndexChanged += ColumnChanged;
			grid.ColumnStateChanged += ColumnStateChanged;
			grid.RowStateChanged += RowStateChanged;
			grid.Sorted += StructureChanged;
			grid.CurrentCellChanged += FocusChanged;
			grid.GotFocus += FocusChanged;
			grid.LostFocus += FocusChanged;
			grid.SelectionChanged += SelectionChanged;
			grid.CellValueChanged += ValueChanged;
			grid.ControlAdded += NativeChildrenChanged;
			grid.ControlRemoved += NativeChildrenChanged;
			grid.Disposed += GridDisposed;
		}

		internal NSObject GetElement(AccessibleObject accessibleObject)
		{
			if (!IsLive || accessibleObject == null) return null;
			if (ReferenceEquals(accessibleObject, Grid.AccessibilityObject)) return View;
			if (accessibleObject is not IDataGridViewAccessibleNode node || node.Grid != Grid) return null;
			if (!elements.TryGetValue(accessibleObject, out var element))
			{
				element = new AccessibleObjectElement(this, accessibleObject);
				if (!element.IsLive) { element.Dispose(); return null; }
				elements.Add(accessibleObject, element);
			}
			return element.IsLive ? element : null;
		}
		internal NSObject[] GetChildren(AccessibleObject accessibleObject)
		{
			int count = Math.Max(0, accessibleObject.GetChildCount());
			if (count == 0) return Array.Empty<NSObject>();
			var children = new List<NSObject>(count);
			for (int i = 0; i < count; ++i)
				if (GetElement(accessibleObject.GetChild(i)) is NSObject child) children.Add(child);
			return children.ToArray();
		}
		internal void CheckHeaders()
		{
			if (columnHeadersVisible == Grid.ColumnHeadersVisible && rowHeadersVisible == Grid.RowHeadersVisible) return;
			bool rowHeaderChanged = rowHeadersVisible != Grid.RowHeadersVisible;
			columnHeadersVisible = Grid.ColumnHeadersVisible;
			rowHeadersVisible = Grid.RowHeadersVisible;
			InvalidateStructure(rowHeaderChanged);
		}
		internal NSObject[] Rows
		{
			get
			{
				if (!IsLive) return Array.Empty<NSObject>();
				CheckHeaders();
				if (rows == null)
				{
					var all = ManagedChildren;
					int start = Grid.ColumnHeadersVisible ? 1 : 0;
					rows = new NSObject[Math.Max(0, all.Length - start)];
					Array.Copy(all, start, rows, 0, rows.Length);
				}
				return rows;
			}
		}
		private NSObject[] ManagedChildren
		{
			get
			{
				CheckHeaders();
				return managedChildren ??= GetChildren(Grid.AccessibilityObject);
			}
		}
		internal NSObject[] ColumnHeaders
		{
			get
			{
				var all = ManagedChildren;
				return Grid.ColumnHeadersVisible && all.Length != 0 && all[0] is AccessibleObjectElement header
					? header.AccessibilityChildren : Array.Empty<NSObject>();
			}
		}
		internal NSObject[] Children(NSObject[] native)
		{
			var managed = IsLive ? ManagedChildren : Array.Empty<NSObject>();
			native ??= Array.Empty<NSObject>();
			bool same = nativeChildren != null && nativeChildren.Length == native.Length;
			for (int i = 0; same && i < native.Length; ++i) same = ReferenceEquals(nativeChildren[i], native[i]);
			if (combinedChildren == null || !same)
			{
				nativeChildren = native;
				combinedChildren = new NSObject[managed.Length + native.Length];
				Array.Copy(managed, combinedChildren, managed.Length);
				Array.Copy(native, 0, combinedChildren, managed.Length, native.Length);
			}
			return combinedChildren;
		}
		internal NSObject FocusedElement => IsLive && Grid.ContainsFocus ?
			(Grid.EditingControl is Control editor && editor.ContainsFocus ? editor.Handle.ToNSView() : GetElement(Grid.AccessibilityObject.GetFocused())) : null;
		internal NSObject[] SelectedCells => selectedCells ??= GetSelection(false);
		internal NSObject[] SelectedRows => selectedRows ??= GetSelection(true);
		private NSObject[] GetSelection(bool selectRows)
		{
			if (!IsLive) return Array.Empty<NSObject>();
			var result = new List<NSObject>();
			if (selectRows)
			{
				foreach (DataGridViewRow row in Grid.SelectedRows)
					if (row.Visible && GetElement(row.AccessibilityObject) is NSObject item) result.Add(item);
			}
			else
			{
				foreach (DataGridViewCell cell in Grid.SelectedCells)
					if (cell.Visible && GetElement(cell.AccessibilityObject) is NSObject item) result.Add(item);
			}
			return result.ToArray();
		}
		internal int RowIndex(DataGridViewRow row)
		{
			if (row == null) return -1;
			int index = 0;
			for (int i = 0; i < row.Index; ++i) if (Grid.Rows[i].Visible) ++index;
			return index;
		}
		internal int ColumnIndex(DataGridViewColumn column)
		{
			if (column == null) return -1;
			int index = 0;
			foreach (DataGridViewColumn other in Grid.Columns)
				if (other.Visible && other.DisplayIndex < column.DisplayIndex) ++index;
			return index;
		}
		internal int RowCount => IsLive ? Grid.Rows.GetRowCount(DataGridViewElementStates.Visible) : 0;
		internal int ColumnCount
		{
			get
			{
				int count = 0;
				if (IsLive) foreach (DataGridViewColumn column in Grid.Columns) if (column.Visible) ++count;
				return count;
			}
		}
		internal NSObject CellAt(nint column, nint row)
		{
			if (!IsLive || column < 0 || row < 0) return null;
			DataGridViewRow target = null;
			int index = 0;
			foreach (DataGridViewRow candidate in Grid.Rows)
				if (candidate.Visible && index++ == row) { target = candidate; break; }
			if (target == null) return null;
			index = 0;
			foreach (DataGridViewColumn current in Grid.Columns.ColumnDisplayIndexSortedArrayList)
				if (current.Visible && index++ == column)
					return GetElement(target.Cells[current.Index].AccessibilityObject);
			return null;
		}
		internal NSObject HitTest(CGPoint point)
		{
			if (!IsLive) return null;
			var screen = Driver.NativeToMonoScreen(point);
			if (!Grid.RectangleToScreen(Grid.ClientRectangle).Contains(screen)) return null;
			var client = Grid.PointToClient(screen);
			var hit = Grid.HitTest(client.X, client.Y);
			if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
				return GetElement(Grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex].AccessibilityObject);
			if (hit.ColumnIndex >= 0 && hit.Type == DataGridViewHitTestType.ColumnHeader)
				return GetElement(Grid.Columns[hit.ColumnIndex].HeaderCell.AccessibilityObject);
			if (hit.RowIndex >= 0 && hit.Type == DataGridViewHitTestType.RowHeader)
				return GetElement(Grid.Rows[hit.RowIndex].HeaderCell.AccessibilityObject);
			return View;
		}
		private void InvalidateStructure(bool childMembershipChanged = false)
		{
			if (!IsLive) return;
			if (childMembershipChanged) ++ChildrenVersion;
			rows = managedChildren = combinedChildren = selectedRows = selectedCells = null;
			List<AccessibleObject> stale = null;
			foreach (var pair in elements)
				if (!pair.Value.IsLive)
				{
					(stale ??= new List<AccessibleObject>()).Add(pair.Key);
					NSAccessibility.PostNotification(pair.Value, NSAccessibilityElement.UIElementDestroyedNotification);
					pair.Value.Detach();
				}
			if (stale != null) foreach (var key in stale) elements.Remove(key);
			NSAccessibility.PostNotification(View, NSAccessibilityElement.LayoutChangedNotification);
		}
		private void RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) => InvalidateStructure();
		private void RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) => InvalidateStructure();
		private void ColumnChanged(object sender, DataGridViewColumnEventArgs e) => InvalidateStructure(true);
		private void ColumnStateChanged(object sender, DataGridViewColumnStateChangedEventArgs e)
		{
			if ((e.StateChanged & DataGridViewElementStates.Visible) != 0) InvalidateStructure(true);
		}
		private void RowStateChanged(object sender, DataGridViewRowStateChangedEventArgs e)
		{
			if ((e.StateChanged & DataGridViewElementStates.Visible) != 0) InvalidateStructure();
		}
		private void StructureChanged(object sender, EventArgs e) => InvalidateStructure();
		private void NativeChildrenChanged(object sender, ControlEventArgs e) => combinedChildren = null;
		private void SelectionChanged(object sender, EventArgs e)
		{
			selectedCells = selectedRows = null;
			if (IsLive) NSAccessibility.PostNotification(View, NSAccessibilityElement.SelectedChildrenChangedNotification);
		}
		private void FocusChanged(object sender, EventArgs e)
		{
			if (IsLive) NSAccessibility.PostNotification(FocusedElement ?? View, NSAccessibilityElement.UIElementFocusedChangedNotification);
		}
		private void ValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			if (!IsLive) return;
			if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
			// Do not instantiate offscreen cells solely to announce a measurement.
			var cell = Grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
			foreach (var pair in elements)
				if (pair.Key is IDataGridViewAccessibleNode node && ReferenceEquals(node.Cell, cell))
				{
					NSAccessibility.PostNotification(pair.Value, NSAccessibilityElement.ValueChangedNotification);
					break;
				}
		}
		private void GridDisposed(object sender, EventArgs e) => Dispose();
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			Grid.RowsAdded -= RowsAdded;
			Grid.RowsRemoved -= RowsRemoved;
			Grid.ColumnAdded -= ColumnChanged;
			Grid.ColumnRemoved -= ColumnChanged;
			Grid.ColumnDisplayIndexChanged -= ColumnChanged;
			Grid.ColumnStateChanged -= ColumnStateChanged;
			Grid.RowStateChanged -= RowStateChanged;
			Grid.Sorted -= StructureChanged;
			Grid.CurrentCellChanged -= FocusChanged;
			Grid.GotFocus -= FocusChanged;
			Grid.LostFocus -= FocusChanged;
			Grid.SelectionChanged -= SelectionChanged;
			Grid.CellValueChanged -= ValueChanged;
			Grid.ControlAdded -= NativeChildrenChanged;
			Grid.ControlRemoved -= NativeChildrenChanged;
			Grid.Disposed -= GridDisposed;
			foreach (var element in elements.Values) { element.Detach(); element.Dispose(); }
			elements.Clear();
			rows = managedChildren = combinedChildren = nativeChildren = selectedCells = selectedRows = null;
		}
	}
}
