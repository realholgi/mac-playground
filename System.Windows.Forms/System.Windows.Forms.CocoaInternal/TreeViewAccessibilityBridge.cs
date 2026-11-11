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
	internal sealed class TreeViewAccessibilityBridge : IDisposable
	{
		private readonly NSView view;
		private readonly XplatUICocoa driver;
		private readonly TreeView tree;
		private readonly Dictionary<TreeNode, NodeElement> elements = new(ReferenceEqualityComparer.Instance);
		private bool disposed;
		private NSObject[] children;
		private NSObject[] rows;
		private NSObject[] selectedRows;
		private TreeNode selectedNode;
		private int childrenVersion;
		private bool IsLive => !disposed && !tree.IsDisposed;

		internal TreeViewAccessibilityBridge(NSView view, XplatUICocoa driver, TreeView control)
		{
			this.view = view;
			this.driver = driver;
			tree = control;
			tree.UIACollectionChanged += CollectionChanged;
			tree.UIANodeTextChanged += ValueChanged;
			tree.UIACheckBoxesChanged += LayoutChanged;
			tree.AfterExpand += Expanded;
			tree.AfterCollapse += Collapsed;
			tree.AfterSelect += SelectionChanged;
			tree.AfterCheck += ValueChanged;
			tree.GotFocus += FocusChanged;
			tree.LostFocus += FocusChanged;
			tree.VisibleChanged += StructureChanged;
			tree.UIANodesSorted += StructureChanged;
			tree.EnabledChanged += LayoutChanged;
			tree.Invalidated += Invalidated;
			tree.Disposed += TreeDisposed;
		}

		internal NSObject[] Children => IsLive && tree.Visible ? children ??= GetChildren(tree.Nodes) : Array.Empty<NSObject>();
		internal NSObject FocusedElement => IsLive && tree.ContainsFocus && IsExposed(tree.SelectedNode) ? GetElement(tree.SelectedNode) : null;
		internal NSObject[] SelectedRows
		{
			get
			{
				TreeNode current = IsLive && IsExposed(tree.SelectedNode) ? tree.SelectedNode : null;
				if (current == null) return Array.Empty<NSObject>();
				if (selectedRows == null || !ReferenceEquals(selectedNode, current))
				{
					selectedNode = current;
					selectedRows = new NSObject[] { GetElement(current) };
				}
				return selectedRows;
			}
		}
		// AXRows is the flattened disclosed outline; AXChildren retains node hierarchy.
		internal NSObject[] Rows
		{
			get
			{
				if (!IsLive || !tree.Visible) return Array.Empty<NSObject>();
				if (rows == null)
				{
					var result = new List<NSObject>();
					AddRows(tree.Nodes, result);
					rows = result.ToArray();
				}
				return rows;
			}
		}
		private void AddRows(TreeNodeCollection nodes, List<NSObject> rows)
		{
			foreach (TreeNode node in nodes)
			{
				rows.Add(GetElement(node));
				if (node.IsExpanded) AddRows(node.Nodes, rows);
			}
		}
		private bool IsAttached(TreeNode node) => IsLive && node != null && ReferenceEquals(node.TreeView, tree);
		private bool IsExposed(TreeNode node)
		{
			if (!IsAttached(node) || !tree.Visible) return false;
			for (TreeNode parent = node.Parent; parent != null; parent = parent.Parent)
				if (!parent.IsExpanded) return false;
			return true;
		}
		private NodeElement GetElement(TreeNode node)
		{
			if (!IsAttached(node)) return null;
			if (!elements.TryGetValue(node, out var element)) elements.Add(node, element = new NodeElement(this, node));
			return element;
		}
		private NSObject[] GetChildren(TreeNodeCollection nodes)
		{
			if (!tree.Visible || nodes.Count == 0) return Array.Empty<NSObject>();
			var children = new NSObject[nodes.Count];
			for (int i = 0; i < children.Length; ++i) children[i] = GetElement(nodes[i]);
			return children;
		}
		internal NSObject HitTest(CGPoint nativeScreenPoint)
		{
			if (!IsLive || !tree.Visible) return null;
			Point point = tree.PointToClient(driver.NativeToMonoScreen(nativeScreenPoint));
			if (!tree.ClientRectangle.Contains(point)) return null;
			TreeNode node = tree.GetNodeAt(point);
			return IsExposed(node) ? GetElement(node) : view;
		}
		private void CollectionChanged(object sender, CollectionChangeEventArgs e)
		{
			// Removal is observed immediately, so a reinserted node cannot revive a stale proxy.
			List<TreeNode> removed = null;
			foreach (var pair in elements)
				if (!IsAttached(pair.Key))
				{
					pair.Value.Detach();
					NSAccessibility.PostNotification(pair.Value, NSAccessibilityElement.UIElementDestroyedNotification);
					(removed ??= new()).Add(pair.Key);
				}
			if (removed != null) foreach (TreeNode node in removed) elements.Remove(node);
			StructureChanged(sender, EventArgs.Empty);
		}
		private void Expanded(object sender, TreeViewEventArgs e) => DisclosureChanged(e.Node, NSAccessibilityElement.RowExpandedNotification);
		private void Collapsed(object sender, TreeViewEventArgs e) => DisclosureChanged(e.Node, NSAccessibilityElement.RowCollapsedNotification);
		private void DisclosureChanged(TreeNode node, NSString notification)
		{
			if (!IsLive) return;
			if (elements.TryGetValue(node, out var element)) NSAccessibility.PostNotification(element, notification);
			StructureChanged(tree, EventArgs.Empty);
		}
		private void SelectionChanged(object sender, TreeViewEventArgs e)
		{
			if (!IsLive) return;
			selectedRows = null;
			selectedNode = null;
			NSAccessibility.PostNotification(view, NSAccessibilityElement.SelectedChildrenChangedNotification);
			FocusChanged(sender, e);
		}
		private void ValueChanged(object sender, TreeViewEventArgs e)
		{
			if (IsLive && elements.TryGetValue(e.Node, out var element))
				NSAccessibility.PostNotification(element, NSAccessibilityElement.ValueChangedNotification);
		}
		private void FocusChanged(object sender, EventArgs e)
		{
			if (IsLive) NSAccessibility.PostNotification(FocusedElement ?? view, NSAccessibilityElement.UIElementFocusedChangedNotification);
		}
		private void LayoutChanged(object sender, EventArgs e)
		{
			if (IsLive) NSAccessibility.PostNotification(view, NSAccessibilityElement.LayoutChangedNotification);
		}
		private void StructureChanged(object sender, EventArgs e)
		{
			++childrenVersion;
			children = rows = null;
			LayoutChanged(sender, e);
		}
		private void Invalidated(object sender, InvalidateEventArgs e) => LayoutChanged(sender, e);
		private void TreeDisposed(object sender, EventArgs e) => Dispose();
		public void Dispose()
		{
			if (disposed) return;
			disposed = true;
			tree.UIACollectionChanged -= CollectionChanged;
			tree.UIANodeTextChanged -= ValueChanged;
			tree.UIACheckBoxesChanged -= LayoutChanged;
			tree.AfterExpand -= Expanded;
			tree.AfterCollapse -= Collapsed;
			tree.AfterSelect -= SelectionChanged;
			tree.AfterCheck -= ValueChanged;
			tree.GotFocus -= FocusChanged;
			tree.LostFocus -= FocusChanged;
			tree.VisibleChanged -= StructureChanged;
			tree.UIANodesSorted -= StructureChanged;
			tree.EnabledChanged -= LayoutChanged;
			tree.Invalidated -= Invalidated;
			tree.Disposed -= TreeDisposed;
			foreach (NodeElement element in elements.Values) element.Detach();
			elements.Clear();
			children = rows = selectedRows = null;
			selectedNode = null;
		}

		private sealed class NodeElement(TreeViewAccessibilityBridge bridge, TreeNode node) : NSAccessibilityElement
		{
			private bool detached;
			private NSObject[] children;
			private int childrenVersion = -1;
			private bool IsLive => !detached && bridge.IsAttached(node);
			private bool IsExposed => IsLive && bridge.IsExposed(node);
			private bool CanAct => IsExposed && bridge.tree.Enabled;
			private bool CanExpand => CanAct && node.Nodes.Count != 0;
			private bool CanCheck => CanAct && bridge.tree.CheckBoxes;
			internal void Detach()
			{
				detached = true;
				children = null;
			}
			public override bool AccessibilityElement => IsExposed;
			public override string AccessibilityRole => IsLive ? "AXRow" : null;
			public override string AccessibilityTitle => IsLive ? node.Text : string.Empty;
			public override string AccessibilityLabel => AccessibilityTitle;
			public override bool AccessibilityEnabled => CanAct;
			public override bool AccessibilityHidden => !IsExposed;
			public override NSObject AccessibilityParent => IsLive ? node.Parent == null ? bridge.view : bridge.GetElement(node.Parent) : null;
			public override NSObject AccessibilityWindow => IsLive ? bridge.view.Window : null;
			public override NSObject AccessibilityTopLevelUIElement => AccessibilityWindow;
			public override NSObject[] AccessibilityChildren
			{
				get
				{
					if (!IsExposed || !node.IsExpanded) return Array.Empty<NSObject>();
					if (childrenVersion != bridge.childrenVersion)
					{
						children = bridge.GetChildren(node.Nodes);
						childrenVersion = bridge.childrenVersion;
					}
					return children;
				}
			}
			public override CGRect AccessibilityFrame => IsExposed && node.IsVisible ? bridge.driver.MonoToNativeScreen(bridge.tree.RectangleToScreen(node.Bounds)) : CGRect.Empty;
			public override bool AccessibilityExpanded
			{
				get => IsLive && node.Nodes.Count != 0 && node.IsExpanded;
				set { if (CanExpand) { if (value) node.Expand(); else node.Collapse(); } }
			}
			public override bool AccessibilityDisclosed
			{
				get => AccessibilityExpanded;
				set => AccessibilityExpanded = value;
			}
			public override bool AccessibilitySelected
			{
				get => IsExposed && ReferenceEquals(bridge.tree.SelectedNode, node);
				set { if (CanAct) { if (value) bridge.tree.SelectedNode = node; else if (AccessibilitySelected) bridge.tree.SelectedNode = null; } }
			}
			public override bool AccessibilityFocused
			{
				get => AccessibilitySelected && bridge.tree.ContainsFocus;
				set { if (value && CanAct) { bridge.tree.SelectedNode = node; if (IsExposed && AccessibilitySelected) bridge.tree.Focus(); } }
			}
			public override NSObject AccessibilityValue
			{
				get => IsLive && bridge.tree.CheckBoxes ? new NSNumber(node.Checked) : null;
				set { if (CanCheck && value is NSNumber number && (number.DoubleValue == 0 || number.DoubleValue == 1)) node.Checked = number.BoolValue; }
			}
			public override bool AccessibilityPerformPress()
			{
				if (!CanAct) return false;
				if (CanCheck) { bool previous = node.Checked; node.Checked = !previous; return node.Checked != previous; }
				bridge.tree.SelectedNode = node;
				return AccessibilitySelected;
			}
			public override bool IsAccessibilitySelectorAllowed(Selector selector)
			{
				switch (selector.Name)
				{
					case "setAccessibilityExpanded:":
					case "setAccessibilityDisclosed:": return CanExpand;
					case "setAccessibilityValue:": return CanCheck;
					case "setAccessibilitySelected:":
					case "setAccessibilityFocused:":
					case "accessibilityPerformPress": return CanAct;
				}
				return base.IsAccessibilitySelectorAllowed(selector);
			}
			[Export("accessibilityIsAttributeSettable:")]
			public bool AccessibilityIsAttributeSettable(NSString attribute) => attribute?.ToString() switch
			{
				"AXExpanded" or "AXDisclosing" => CanExpand,
				"AXValue" => CanCheck,
				"AXSelected" or "AXFocused" => CanAct,
				_ => false
			};
		}
	}
}
