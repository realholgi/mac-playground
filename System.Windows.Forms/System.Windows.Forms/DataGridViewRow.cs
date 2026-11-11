// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
//
// Author:
//	Pedro Martínez Juliá <pedromj@gmail.com>
//


using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

namespace System.Windows.Forms
{
	[TypeConverter (typeof (DataGridViewRowConverter))]
	public class DataGridViewRow : DataGridViewBand
	{
		private AccessibleObject accessibilityObject;
		internal bool AccessibilityDisposed { get; private set; }
		private DataGridViewCellCollection cells;
		private ContextMenuStrip contextMenuStrip;
		private int dividerHeight;
		private string errorText;
		private DataGridViewRowHeaderCell headerCell;
		private int height;
		private int minimumHeight;
		private int explicit_height;

		public DataGridViewRow ()
		{
			cells = new DataGridViewCellCollection(this);
			minimumHeight = 3;
			height = -1;
			explicit_height = -1;
			headerCell = new DataGridViewRowHeaderCell();
			headerCell.SetOwningRow (this);
			SetState (DataGridViewElementStates.Visible);
		}

		[Browsable (false)]
		public AccessibleObject AccessibilityObject {
			get { return accessibilityObject ?? (accessibilityObject = CreateAccessibilityInstance ()); }
		}

		[Browsable (false)]
		[DesignerSerializationVisibility (DesignerSerializationVisibility.Content)]
		public DataGridViewCellCollection Cells {
			get { return cells; }
		}

		[DefaultValue (null)]
		public override ContextMenuStrip ContextMenuStrip {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Operation cannot be performed on a shared row.");
					
				return contextMenuStrip;
			}
			set {
				if (contextMenuStrip != value) {
					contextMenuStrip = value;
					if (DataGridView != null) {
						DataGridView.OnRowContextMenuStripChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		[Browsable (false)]
		[EditorBrowsable (EditorBrowsableState.Advanced)]
		public object DataBoundItem {
			get {
				if (base.DataGridView != null && DataGridView.DataManager != null) {
					if (DataGridView.DataManager.Count > base.Index)
						return DataGridView.DataManager[base.Index];
				}
				return null;
			}
		}

		[Browsable (true)]
		[DesignerSerializationVisibility (DesignerSerializationVisibility.Content)]
		[NotifyParentProperty (true)]
		public override DataGridViewCellStyle DefaultCellStyle {
			get { return base.DefaultCellStyle; }
			set {
				if (DefaultCellStyle != value) {
					base.DefaultCellStyle = value;
					if (DataGridView != null) {
						DataGridView.OnRowDefaultCellStyleChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		[Browsable (false)]
		public override bool Displayed {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the Displayed property of a shared row is not a valid operation.");
					
				return base.Displayed;
			}
		}

		[DefaultValue (0)]
		[NotifyParentProperty (true)]
		public int DividerHeight {
			get { return dividerHeight; }
			set { dividerHeight = value; }
		}

		[DefaultValue ("")]
		[NotifyParentProperty (true)]
		public string ErrorText {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Operation cannot be performed on a shared row.");
					
				return errorText == null ? string.Empty : errorText;
			}
			set {
				if (errorText != value) {
					errorText = value;
					if (DataGridView != null) {
						DataGridView.OnRowErrorTextChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		[Browsable (false)]
		public override bool Frozen {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the Frozen property of a shared row is not a valid operation.");
					
				return base.Frozen;
			}
			set { base.Frozen = value; }
		}

		[Browsable (false)]
		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public DataGridViewRowHeaderCell HeaderCell {
			get { return headerCell; }
			set {
				if (headerCell != value) {
					headerCell = value;
					headerCell.SetOwningRow (this);
					
					if (DataGridView != null) {
						headerCell.SetDataGridView (DataGridView);
						DataGridView.OnRowHeaderCellChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		[DefaultValue (22)]
		[NotifyParentProperty (true)]
		public int Height {
			get {
				if (height < 0) {
					if (DefaultCellStyle != null && DefaultCellStyle.Font != null) {
						return DefaultCellStyle.Font.Height + 9;
					}
					if (Index >= 0 && InheritedStyle != null && InheritedStyle.Font != null) {
						return InheritedStyle.Font.Height + 9;
					}
					return System.Windows.Forms.Control.DefaultFont.Height + 9;
				}
				return height;
			}
			set {
				explicit_height = value;
				
				if (height != value) {
					if (value < minimumHeight) {
						height = minimumHeight;
					} else {
						height = value;
					}
					if (DataGridView != null) {
						DataGridView.Invalidate ();
						DataGridView.OnRowHeightChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		public override DataGridViewCellStyle InheritedStyle {
			get {
				if (Index == -1)
					throw new InvalidOperationException ("Getting the InheritedStyle property of a shared row is not a valid operation.");
					
				if (DataGridView == null) {
					return DefaultCellStyle;
				}
				else {
					if (DefaultCellStyle == null) {
						return DataGridView.DefaultCellStyle;
					}
					else {
						DataGridViewCellStyle style = (DataGridViewCellStyle) DefaultCellStyle.Clone();
						/////// Combination with dataGridView.DefaultCellStyle
						return style;
					}
				}
			}
		}

		[Browsable (false)]
		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public bool IsNewRow {
			get {
				if (DataGridView != null && DataGridView.Rows[DataGridView.Rows.Count - 1] == this && DataGridView.NewRowIndex == Index) {
					return true;
				}
				return false;
			}
		}

		internal bool IsShared {
			get {
				return Index == -1 && DataGridView != null;
			}
		}

		[Browsable (false)]
		[DesignerSerializationVisibility (DesignerSerializationVisibility.Hidden)]
		public int MinimumHeight {
			get { return minimumHeight; }
			set {
				if (minimumHeight != value) {
					if (value < 2 || value > Int32.MaxValue) {
						throw new ArgumentOutOfRangeException("MinimumHeight should be between 2 and Int32.MaxValue.");
					}
					if (height < value) {
						// don't let height get less than minimumHeight!
						Height = value;
					}
					minimumHeight = value;
					if (DataGridView != null) {
						DataGridView.OnRowMinimumHeightChanged(new DataGridViewRowEventArgs(this));
					}
				}
			}
		}

		[Browsable (true)]
		[DefaultValue (false)]
		[NotifyParentProperty (true)]
		public override bool ReadOnly {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the ReadOnly property of a shared row is not a valid operation.");
					
				if (DataGridView != null && DataGridView.ReadOnly)
					return true;
					
				return base.ReadOnly;
			}
			set { base.ReadOnly = value; }
		}

		[NotifyParentProperty (true)]
		public override DataGridViewTriState Resizable {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the Resizable property of a shared row is not a valid operation.");
					
				return base.Resizable;
			}
			set { base.Resizable = value; }
		}

		public override bool Selected {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the Selected property of a shared row is not a valid operation.");
					
				return base.Selected;
			}
			set {
				if (Index == -1) {
					throw new InvalidOperationException("The row is a shared row.");
				}
				if (DataGridView == null) {
					throw new InvalidOperationException("The row has not been added to a DataGridView control.");
				}
				base.Selected = value;
			}
		}

		public override DataGridViewElementStates State {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the State property of a shared row is not a valid operation.");
					
				return base.State;
			}
		}

		[Browsable (false)]
		public override bool Visible {
			get {
				if (IsShared)
					throw new InvalidOperationException ("Getting the Visible property of a shared row is not a valid operation.");
					
				return base.Visible;
			}
			set {
				if (IsNewRow && value == false) {
					throw new InvalidOperationException("Cant make invisible a new row.");
				}
				if (!value && DataGridView != null && DataGridView.DataManager != null && 
				    DataGridView.DataManager.Position == Index)
					throw new InvalidOperationException("Row associated with the currency manager's position cannot be made invisible.");

				base.Visible = value;
				if (DataGridView != null)
					DataGridView.Invalidate ();
			}
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		public virtual DataGridViewAdvancedBorderStyle AdjustRowHeaderBorderStyle (DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStyleInput, DataGridViewAdvancedBorderStyle dataGridViewAdvancedBorderStylePlaceholder, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded, bool isFirstDisplayedRow, bool isLastVisibleRow)
		{
			throw new NotImplementedException();
		}

		public override object Clone ()
		{
			DataGridViewRow row = (DataGridViewRow)MemberwiseClone ();
			row.accessibilityObject = null;
			row.AccessibilityDisposed = false;

			row.HeaderCell = (DataGridViewRowHeaderCell)HeaderCell.Clone ();
			row.SetIndex (-1);
			
			row.cells = new DataGridViewCellCollection (row);
			
			foreach (DataGridViewCell cell in cells)
				row.cells.Add (cell.Clone () as DataGridViewCell);

			row.SetDataGridView (null);

			return row;
		}

		public void CreateCells (DataGridView dataGridView)
		{
			if (dataGridView == null) {
				throw new ArgumentNullException("DataGridView is null.");
			}
			if (dataGridView.Rows.Contains(this)) {
				throw new InvalidOperationException("The row already exists in the DataGridView.");
			}
			DataGridViewCellCollection newCellCollection = new DataGridViewCellCollection(this);
			foreach (DataGridViewColumn column in dataGridView.Columns) {
				if (column.CellTemplate == null) {
					throw new InvalidOperationException("Cell template not set in column: " + column.Index.ToString() + ".");
				}
				newCellCollection.Add((DataGridViewCell) column.CellTemplate.Clone());
			}
			cells = newCellCollection;
		}

		public void CreateCells (DataGridView dataGridView, params object[] values)
		{
			if (values == null) {
				throw new ArgumentNullException("values is null");
			}
			CreateCells(dataGridView);
			for (int i = 0; i < values.Length; i++) {
				cells[i].Value = values[i];
			}
		}

		public ContextMenuStrip GetContextMenuStrip (int rowIndex)
		{
			if (rowIndex == -1) {
				throw new InvalidOperationException("rowIndex is -1");
			}
			if (rowIndex < 0 || rowIndex >= DataGridView.Rows.Count) {
				throw new ArgumentOutOfRangeException("rowIndex is out of range");
			}

			return null; // XXX
		}

		public string GetErrorText (int rowIndex)
		{
			return string.Empty;
		}

		public virtual int GetPreferredHeight (int rowIndex, DataGridViewAutoSizeRowMode autoSizeRowMode, bool fixedWidth)
		{
			DataGridViewRow row;
			
			if (DataGridView != null)
				row = DataGridView.Rows.SharedRow (rowIndex);
			else
				row = this;

			int height = 0;

			if (autoSizeRowMode == DataGridViewAutoSizeRowMode.AllCells || autoSizeRowMode == DataGridViewAutoSizeRowMode.RowHeader)
				height = Math.Max (height, row.HeaderCell.PreferredSize.Height);

			if (autoSizeRowMode == DataGridViewAutoSizeRowMode.AllCells || autoSizeRowMode == DataGridViewAutoSizeRowMode.AllCellsExceptHeader)
				foreach (DataGridViewCell cell in row.Cells)
					height = Math.Max (height, cell.PreferredSize.Height);
			
			return height;
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		public virtual DataGridViewElementStates GetState (int rowIndex)
		{
			DataGridViewElementStates state = DataGridViewElementStates.None;
			
			if (rowIndex == -1) {
				state |= DataGridViewElementStates.Displayed;
				
				if (DataGridView.ReadOnly)
					state |= DataGridViewElementStates.ReadOnly;
				if (DataGridView.AllowUserToResizeRows)
					state |= DataGridViewElementStates.Resizable;
				if (DataGridView.Visible)
					state |= DataGridViewElementStates.Visible;
			
				return state;
			}
			
			DataGridViewRow row = DataGridView.Rows[rowIndex];

			if (row.Displayed)
				state |= DataGridViewElementStates.Displayed;
			if (row.Frozen)
				state |= DataGridViewElementStates.Frozen;
			if (row.ReadOnly)
				state |= DataGridViewElementStates.ReadOnly;
			if (row.Resizable == DataGridViewTriState.True || (row.Resizable == DataGridViewTriState.NotSet && DataGridView.AllowUserToResizeRows))
				state |= DataGridViewElementStates.Resizable;
			if (row.Resizable == DataGridViewTriState.True)
				state |= DataGridViewElementStates.ResizableSet;
			if (row.Selected)
				state |= DataGridViewElementStates.Selected;
			if (row.Visible)
				state |= DataGridViewElementStates.Visible;
				
			return state;
		}

		public bool SetValues (params object[] values)
		{
			if (values == null) {
				throw new ArgumentNullException("vues is null");
			}
			if (DataGridView != null && DataGridView.VirtualMode) {
				throw new InvalidOperationException("DataGridView is operating in virtual mode");
			}
			/////// COLUMNAS //////////
			for (int i = 0; i < values.Length; i++) {
				DataGridViewCell cell;
				if (cells.Count > i) {
					cell = cells [i];
				} else {
					cell = new DataGridViewTextBoxCell ();
					cells.Add (cell);
				}
				cell.Value = values[i];
			}
			
			// XXX
			return true;
		}

		public override string ToString ()
		{
			return this.GetType().Name + ", Band Index: " + base.Index.ToString();
		}

		protected virtual AccessibleObject CreateAccessibilityInstance ()
		{
			return new DataGridViewRowAccessibleObject(this);
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		protected virtual DataGridViewCellCollection CreateCellsInstance ()
		{
			cells = new DataGridViewCellCollection(this);
			return cells;
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		protected internal virtual void DrawFocus (Graphics graphics, Rectangle clipBounds, Rectangle bounds, int rowIndex, DataGridViewElementStates rowState, DataGridViewCellStyle cellStyle, bool cellsPaintSelectionBackground)
		{
		}

		protected internal virtual void Paint (Graphics graphics, Rectangle clipBounds, Rectangle rowBounds, int rowIndex, DataGridViewElementStates rowState, bool isFirstDisplayedRow, bool isLastVisibleRow)
		{
			DataGridViewCellStyle style;
			
			if (Index == -1)
				style = DataGridView.RowsDefaultCellStyle;
			else
				style = InheritedStyle;
				
			DataGridViewRowPrePaintEventArgs pre = new DataGridViewRowPrePaintEventArgs (DataGridView, graphics, clipBounds, rowBounds, rowIndex, rowState, string.Empty, style, isFirstDisplayedRow, isLastVisibleRow);
			pre.PaintParts = DataGridViewPaintParts.All;

			DataGridView.OnRowPrePaint (pre);

			// The user has elected for us to not do anything
			if (pre.Handled)
				return;

			if (DataGridView.RowHeadersVisible)
				PaintHeader (graphics, pre.ClipBounds, rowBounds, rowIndex, rowState, isFirstDisplayedRow, isLastVisibleRow, pre.PaintParts);
			
			PaintCells (graphics, pre.ClipBounds, rowBounds, rowIndex, rowState, isFirstDisplayedRow, isLastVisibleRow, pre.PaintParts);

			DataGridViewRowPostPaintEventArgs post = new DataGridViewRowPostPaintEventArgs (DataGridView, graphics, pre.ClipBounds, rowBounds, rowIndex, rowState, pre.ErrorText, style, isFirstDisplayedRow, isLastVisibleRow);
			DataGridView.OnRowPostPaint (post);
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		protected internal virtual void PaintCells (Graphics graphics, Rectangle clipBounds, Rectangle rowBounds, int rowIndex, DataGridViewElementStates rowState, bool isFirstDisplayedRow, bool isLastVisibleRow, DataGridViewPaintParts paintParts)
		{
			List<DataGridViewColumn> sortedColumns = DataGridView.Columns.ColumnDisplayIndexSortedArrayList;
			
			Rectangle bounds = rowBounds;
			
			// If row headers are visible, adjust our starting point
			if (DataGridView.RowHeadersVisible) {
				bounds.X += DataGridView.RowHeadersWidth;
				bounds.Width -= DataGridView.RowHeadersWidth;
			}
			
			for (int i = DataGridView.first_col_index; i < sortedColumns.Count; i++) {
				DataGridViewColumn col = sortedColumns[i];
				
				if (!col.Visible)
					continue;
					
				if (!col.Displayed)
					break;
					
				bounds.Width = col.Width;
				DataGridViewCell cell = Cells[col.Index];
				
				if ((paintParts & DataGridViewPaintParts.Background) == DataGridViewPaintParts.Background)
					graphics.FillRectangle (Brushes.White, bounds);
				
				DataGridViewCellStyle style;

				if (cell.RowIndex == -1)
					style = DefaultCellStyle;
				else
					style = cell.InheritedStyle;

				object value;
				DataGridViewElementStates cellState;
				
				if (cell.RowIndex == -1) {
					// TODO: Look up value if databound.
					value = null;
					cellState = cell.State;
				} else {
					value = cell.Value;
					cellState = cell.InheritedState;
				}

				DataGridViewAdvancedBorderStyle intermediateBorderStyle = (DataGridViewAdvancedBorderStyle)((ICloneable)DataGridView.AdvancedCellBorderStyle).Clone ();
				DataGridViewAdvancedBorderStyle borderStyle = cell.AdjustCellBorderStyle (DataGridView.AdvancedCellBorderStyle, intermediateBorderStyle, true, true, cell.ColumnIndex == 0, cell.RowIndex == 0);
				DataGridView.OnCellFormattingInternal (new DataGridViewCellFormattingEventArgs (cell.ColumnIndex, cell.RowIndex, value, cell.FormattedValueType, style));


				cell.PaintWork (graphics, clipBounds, bounds, rowIndex, cellState, style, borderStyle, paintParts);
				bounds.X += bounds.Width;
			}
		}

		[EditorBrowsable (EditorBrowsableState.Advanced)]
		protected internal virtual void PaintHeader (Graphics graphics, Rectangle clipBounds, Rectangle rowBounds, int rowIndex, DataGridViewElementStates rowState, bool isFirstDisplayedRow, bool isLastVisibleRow, DataGridViewPaintParts paintParts)
		{
			rowBounds.Width = DataGridView.RowHeadersWidth;
			graphics.FillRectangle (Brushes.White, rowBounds);
	
			HeaderCell.PaintWork (graphics, clipBounds, rowBounds, rowIndex, rowState, HeaderCell.InheritedStyle, DataGridView.AdvancedRowHeadersBorderStyle, paintParts);
		}

		internal override void SetDataGridView (DataGridView dataGridView)
		{
			base.SetDataGridView(dataGridView);
			headerCell.SetDataGridView(dataGridView);
			foreach (DataGridViewCell cell in cells)
				cell.SetDataGridView (dataGridView);
		}

		internal override void SetState (DataGridViewElementStates state)
		{
			if (State != state) {
				base.SetState(state);
				if (DataGridView != null) {
					DataGridView.OnRowStateChanged(this.Index, new DataGridViewRowStateChangedEventArgs(this, state));
				}
			}
		}
		
		// Set the row's height without overwriting the explicit_height, so we
		// can go back to the user's requested height when they turn off AutoSize
		internal void SetAutoSizeHeight (int height)
		{
			this.height = height;
			
			if (DataGridView != null) {
				DataGridView.Invalidate ();
				DataGridView.OnRowHeightChanged (new DataGridViewRowEventArgs (this));
			}
		}

		// If the user sets AutoSizeRowMode to None, reset every row to its explicit height
		internal void ResetToExplicitHeight ()
		{
			this.height = explicit_height;

			if (DataGridView != null)
				DataGridView.OnRowHeightChanged (new DataGridViewRowEventArgs (this));
		}
		
		protected override void Dispose (bool disposing)
		{
			AccessibilityDisposed = true;
			base.Dispose (disposing);
		}

		[ComVisibleAttribute(true)]
		protected class DataGridViewRowAccessibleObject : AccessibleObject, IDataGridViewAccessibleNode {
			private DataGridViewRow dataGridViewRow;
			public DataGridViewRowAccessibleObject () {}
			public DataGridViewRowAccessibleObject (DataGridViewRow owner) { dataGridViewRow = owner; }
			public DataGridViewRow Owner {
				get { return dataGridViewRow; }
				set { dataGridViewRow = value; }
			}
			public DataGridView Grid { get { return Owner == null ? null : Owner.DataGridView; } }
			public DataGridViewRow Row { get { return Owner; } }
			public DataGridViewCell Cell { get { return null; } }
			private bool Available { get { return Grid != null && Grid.AccessibilityHasRow (Owner); } }
			private bool CanSelect { get { return Available && Grid.Enabled && Grid.Visible && Owner.Visible; } }
			public override Rectangle Bounds {
				get { return Available && Owner.Visible ? Grid.RectangleToScreen (Grid.GetRowDisplayRectangle (Owner.Index, false)) : Rectangle.Empty; }
			}
			public override string Name { get { return Available ? (Owner.Index + 1).ToString () : string.Empty; } }
			public override AccessibleObject Parent { get { return Available ? Grid.AccessibilityObject : null; } }
			public override AccessibleRole Role { get { return AccessibleRole.Row; } }
			public override AccessibleStates State {
				get {
					if (!Available)
						return AccessibleStates.Unavailable | AccessibleStates.Offscreen;
					AccessibleStates state = Grid.AccessibilityState (Bounds) | AccessibleStates.Selectable;
					if (CanSelect)
						state |= AccessibleStates.Focusable;
					if (!Owner.Visible)
						state |= AccessibleStates.Invisible | AccessibleStates.Offscreen;
					if (Grid.ReadOnly || Owner.ReadOnly)
						state |= AccessibleStates.ReadOnly;
					if (Owner.Selected)
						state |= AccessibleStates.Selected;
					if (Grid.ContainsFocus && Grid.CurrentRow == Owner)
						state |= AccessibleStates.Focused;
					return state;
				}
			}
			public override string Value { get { return string.Empty; } }
			public override int GetChildCount ()
			{
				return Available && Owner.Visible ? Grid.AccessibilityColumnCount + (Grid.RowHeadersVisible ? 1 : 0) : 0;
			}
			public override AccessibleObject GetChild (int index)
			{
				if (index < 0 || !Available || !Owner.Visible)
					return null;
				if (Grid.RowHeadersVisible && index-- == 0)
					return Owner.HeaderCell.AccessibilityObject;
				DataGridViewColumn column = Grid.AccessibilityColumn (index);
				return column == null ? null : Owner.Cells [column.Index].AccessibilityObject;
			}
			public override AccessibleObject GetFocused ()
			{
				return Available && Grid.ContainsFocus && Grid.CurrentRow == Owner && Grid.CurrentCell.Visible ?
					Grid.CurrentCell.AccessibilityObject : null;
			}
			public override AccessibleObject GetSelected ()
			{
				if (!Available || !Owner.Visible)
					return null;
				if (Owner.Selected)
					return this;
				for (int i = 0; i < GetChildCount (); i++) {
					AccessibleObject child = GetChild (i);
					if ((child.State & AccessibleStates.Selected) != 0)
						return child;
				}
				return null;
			}
			public override AccessibleObject Navigate (AccessibleNavigation direction)
			{
				if (!Available)
					return null;
				if (direction == AccessibleNavigation.FirstChild)
					return GetChild (0);
				if (direction == AccessibleNavigation.LastChild)
					return GetChild (GetChildCount () - 1);
				int step;
				if (direction == AccessibleNavigation.Next || direction == AccessibleNavigation.Down)
					step = 1;
				else if (direction == AccessibleNavigation.Previous || direction == AccessibleNavigation.Up)
					step = -1;
				else
					return null;
				for (int i = Owner.Index + step; i >= 0 && i < Grid.Rows.Count; i += step)
					if (Grid.Rows [i].Visible)
						return Grid.Rows [i].AccessibilityObject;
				return step < 0 && Grid.ColumnHeadersVisible ? Grid.AccessibilityHeaderRow : null;
			}
			public override void Select (AccessibleSelection flags)
			{
				if (!CanSelect)
					return;
				if ((flags & AccessibleSelection.TakeFocus) != 0) {
					DataGridViewColumn column = Grid.AccessibilityColumn (0);
					if (column == null)
						return;
					Grid.Focus ();
					Grid.CurrentCell = Owner.Cells [column.Index];
					if (Grid.CurrentRow != Owner)
						return;
				}
				if ((flags & AccessibleSelection.TakeSelection) != 0)
					Grid.ClearSelection ();
				if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.AddSelection)) != 0)
					Owner.Selected = true;
				if ((flags & AccessibleSelection.RemoveSelection) != 0)
					Owner.Selected = false;
			}
		}
	}
	
	internal class DataGridViewRowConverter : TypeConverter
	{
	}
}

