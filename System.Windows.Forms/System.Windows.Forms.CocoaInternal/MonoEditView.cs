using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms.Mac;
using Foundation;
using AppKit;
using CoreGraphics;
using ObjCRuntime;

namespace System.Windows.Forms.CocoaInternal
{
	//[ExportClass("MonoEditView", "MonoView")]
	class MonoEditView : MonoView, INSTextInputClient
	{
		internal NSAttributedString markedText = new NSMutableAttributedString();
		internal string insertText = String.Empty;
		internal bool isComposing;

		public MonoEditView(NativeHandle instance) : base(instance)
		{
		}

		public MonoEditView(XplatUICocoa driver, CGRect frameRect, WindowStyles style, WindowExStyles exStyle) : base(driver, frameRect, style, exStyle)
		{
		}

		#region INSTextInputClient

		public bool HasMarkedText
		{
			// Until this returns false, interpretKeyEvents continues to call insertText !
			get
			{
				var result = markedText.Length != 0;
				//Debug.WriteLine($"hasMarkedText -> {result}");
				return result;
			}
		}

		public NSRange MarkedRange
		{
			get
			{
				GetSelection(out var start, out var _);
				start = Math.Max(0, start - (int)markedText.Length);
				var result = new NSRange(start, markedText.Length);
				//Debug.WriteLine($"markedRange -> {result}");
				return result;
			}
		}

		public NSRange SelectedRange
		{
			get
			{
				GetSelection(out var start, out var end);
				var result = new NSRange(start, Math.Max(0, end - start));
				//Debug.WriteLine($"selectedRange -> {result}");
				return result;
			}
			set 
			{
				var range = ClampRange(value, GetText().Length);
				SetSelection((int)range.Location, (int)(range.Location + range.Length));
			}
		}

		public NSString[] ValidAttributesForMarkedText
		{
			get { return validAttributesForMarkedText; }
		}

		public NSWindowLevel WindowLevel
		{
			get { return Window?.Level ?? NSWindowLevel.Normal; }
		}

		public NSAttributedString GetAttributedSubstring(NSRange proposedRange, out NSRange actualRange)
		{
			var text = IsPassword ? string.Empty : GetText();
			actualRange = ClampRange(proposedRange, text.Length);
			return new NSAttributedString(text.Substring((int)actualRange.Location, (int)actualRange.Length));
		}

		public nuint GetCharacterIndex(CGPoint point)
		{
			this.NotImplemented(MethodBase.GetCurrentMethod());
			return 0;
		}

		public CGRect GetFirstRect(NSRange characterRange, out NSRange actualRange)
		{
			actualRange = new NSRange();

			// SWF textboxes
			if (XplatUICocoa.CaretView != null && XplatUICocoa.CaretView.Superview == this)
				return Window.ConvertRectToScreen(XplatUICocoa.CaretView.ConvertRectToView(Bounds, null));

			// EMC textboxes - teporary solution
			var first = new CGRect(Bounds.Location, CGSize.Empty);
			var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XplatUIWin32.RECT)));
			var result = driver.SendMessage(Handle, Msg.WM_IME_GETCURRENTPOSITION, IntPtr.Zero, ptr);
			if (result == (IntPtr)1 && Marshal.PtrToStructure<XplatUIWin32.RECT>(ptr) is XplatUIWin32.RECT r)
				first = new CGRect(first.Location.X + r.left, first.Location.Y + r.top, r.right - r.left, r.bottom - r.top);
			Marshal.FreeHGlobal(ptr);
			return Window.ConvertRectToScreen(ConvertRectToView(first, null));
		}

		public void InsertText(NSObject text, NSRange replacementRange)
		{
			//Console.WriteLine($"insertText:{text}, replacementRange:{replacementRange}, isComposing:{isComposing}");

			if (isComposing)
			{
				isComposing = false;
				eventResponder.OnImeCompositionEnd();

				// Commit IME session
				insertText = text.ToString();
				markedText = new NSAttributedString();
				var lParam = new IntPtr((int)(GCS.GCS_RESULTSTR | GCS.GCS_COMPATTR | GCS.GCS_COMPCLAUSE | GCS.GCS_CURSORPOS));
				Application.SendMessage(Handle, Msg.WM_IME_COMPOSITION, IntPtr.Zero, lParam);
				Application.SendMessage(Handle, Msg.WM_IME_ENDCOMPOSITION, IntPtr.Zero, lParam);

				// There's probably no notification in macOS text input system about closing the candidate window, so here we emulate it. lParam is not set, however.
				Application.SendMessage(Handle, Msg.WM_IME_NOTIFY, new IntPtr((int)Imn.IMN_CLOSECANDIDATE), IntPtr.Zero);
			}
			else
			{
				eventResponder.InsertText(text, replacementRange);
			}
		}

		public void SetMarkedText(NSObject text, NSRange selectedRange, NSRange replacementRange)
		{
			//Console.WriteLine($"setMarkedText:{text}, selectedRange:{selectedRange}, replacementRange:{replacementRange}");

			var value = text is NSAttributedString str ? str : new NSAttributedString(text.ToString());

			// Start session
			if (markedText.Length == 0 && value.Length != 0)
			{
				eventResponder.OnImeCompositionBegin();

				// There's probably no notification in macOS text input system about opening the candidate window, so here we emulate it. lParam is not set, however.
				Application.SendMessage(Handle, Msg.WM_IME_NOTIFY, new IntPtr((int)Imn.IMN_OPENCANDIDATE), IntPtr.Zero);

				Application.SendMessage(Handle, Msg.WM_IME_STARTCOMPOSITION, IntPtr.Zero, IntPtr.Zero);
				insertText = string.Empty;
				isComposing = true;
			}
			else
			{
				eventResponder.OnImeCompositionContinue();
			}

			markedText = value;

			// Continue session
			var lParam = new IntPtr((int)(GCS.GCS_COMPSTR | GCS.GCS_COMPATTR | GCS.GCS_COMPCLAUSE | GCS.GCS_CURSORPOS));
			if (markedText.Length != 0)
				Application.SendMessage(Handle, Msg.WM_IME_COMPOSITION, IntPtr.Zero, lParam);
		}

		public void UnmarkText()
		{
			markedText = new NSMutableAttributedString();
		}

		static readonly NSString[] validAttributesForMarkedText = new NSString[] {
			(NSString)"NSFont",
			(NSString)"NSUnderline",
			(NSString)"NSColor",
			(NSString)"NSBackgroundColor",
			(NSString)"NSUnderlineColor",
		};

		#endregion INSTextInputView

		unsafe public virtual string GetText()
		{
			if (Owner == null)
				return string.Empty;
			var length = Math.Max(0, (int)Application.SendMessage(Handle, Msg.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero));
			var buffer = new char[length + 1];
			fixed (char* ptr = &buffer[0])
			{
				int n = (int)Application.SendMessage(Handle, Msg.WM_GETTEXT, (IntPtr)buffer.Length, new IntPtr(ptr));
				return new string(buffer, 0, Math.Clamp(n, 0, length));
			}
		}

		unsafe public virtual void SetText(string value)
		{
			if (!CanEdit)
				return;
			value ??= string.Empty;
			var selection = SelectedRange;
			fixed (char* ptr = value)
				Application.SendMessage(Handle, Msg.WM_SETTEXT, IntPtr.Zero, new IntPtr(ptr));
			NotifySelectionChanged(selection);
		}

		unsafe public void GetSelection(out int from, out int to)
		{
			if (Owner == null)
			{
				from = to = 0;
				return;
			}
			var range = new int[] { 0, 0 };
			fixed (int* start = &range[0], end = &range[1])
				Application.SendMessage(Handle, Msg.EM_GETSEL, new IntPtr(start), new IntPtr(end));
			
			if (range[0] < range[1])
			{
				from = range[0];
				to = range[1];
			}
			else
			{
				from = range[1];
				to = range[0];
			}
		}

		unsafe public void SetSelection(int from, int to)
		{
			var range = new int[] { Math.Min(from, to), Math.Max(from, to) };
			fixed (int* start = &range[0], end = &range[1])
				Application.SendMessage(Handle, Msg.EM_SETSEL, new IntPtr(start), new IntPtr(end));
		}

		// Accessibility

		// Custom EDIT controls use the WinForms edit-message contract. Ordinary TextBox
		// controls create native NSTextField/NSTextView editors instead of this view.
		Control Owner => Handle != IntPtr.Zero && Control.FromHandle(Handle) is Control c &&
			!c.IsDisposed && !c.Disposing && c.IsHandleCreated ? c : null;

		bool CanSelect => Owner is Control c && c.Enabled && c.Visible;
		bool CanEdit => CanSelect && ((int)Style & 0x0800) == 0 &&
			(Owner.AccessibilityObject.State & AccessibleStates.ReadOnly) == 0 &&
			!(Owner is TextBoxBase box && box.ReadOnly);
		bool IsPassword => ((int)Style & 0x0020) != 0 ||
			Owner is TextBox { PasswordChar: not '\0' } ||
			Owner is TextBox { UseSystemPasswordChar: true };

		static NSRange ClampRange(NSRange range, int length)
		{
			if (range.Location < 0 || range.Length < 0)
				return new NSRange(0, 0);
			var start = (int)Math.Min(range.Location, length);
			return new NSRange(start, Math.Min(range.Length, length - start));
		}

		public override string AccessibilityRole => IsPassword ? "AXTextField" : "AXTextArea";
		public override string AccessibilitySubrole => IsPassword ? "AXSecureTextField" : base.AccessibilitySubrole;

		public override bool IsAccessibilitySelectorAllowed(Selector selector)
		{
			switch (selector.Name)
			{
				case "accessibilityValue": return true;
				case "setAccessibilityValue:":
				case "setAccessibilitySelectedText:": return CanEdit;
				case "setAccessibilitySelectedTextRange:": return CanSelect;
			}
			return base.IsAccessibilitySelectorAllowed(selector);
		}

		public override bool AccessibilityEnabled => Owner?.Enabled ?? false;

		[Export("accessibilityIsValueAttributeSettable")]
		public override bool AccessibilityIsValueAttributeSettable() => CanEdit;

		public override bool AccessibilityIsAttributeSettable(NSString attribute)
		{
			switch (attribute?.ToString())
			{
				case "AXValue":
				case "AXSelectedText": return CanEdit;
				case "AXSelectedTextRange": return CanSelect;
			}
			return base.AccessibilityIsAttributeSettable(attribute);
		}

		public override NSObject AccessibilityValue
		{
			get => new NSString(IsPassword ? string.Empty : GetText());
			set => SetText(value?.ToString() ?? string.Empty);
		}

		[Export("accessibilitySetValue:forAttribute:")]
		public virtual void AccessibilitySetValue(NSObject value, NSString attribute)
		{
			switch (attribute?.ToString())
			{
				case "AXValue": AccessibilityValue = value; break;
				case "AXSelectedText": AccessibilitySelectedText = value?.ToString() ?? string.Empty; break;
				case "AXSelectedTextRange" when value is NSValue range:
					AccessibilitySelectedTextRange = range.RangeValue;
					break;
			}
		}

		public override nint AccessibilityNumberOfCharacters => IsPassword ? 0 : GetText().Length;

		public override string AccessibilitySelectedText
		{
			get
			{
				var text = IsPassword ? string.Empty : GetText();
				var range = ClampRange(SelectedRange, text.Length);
				return text.Substring((int)range.Location, (int)range.Length);
			}
			set => ReplaceSelectedText(value ?? string.Empty);
		}

		unsafe void ReplaceSelectedText(string value)
		{
			if (!CanEdit)
				return;
			var previousText = GetText();
			var selection = SelectedRange;
			SelectedRange = ClampRange(SelectedRange, GetText().Length);
			fixed (char* ptr = value)
				Application.SendMessage(Handle, Msg.EM_REPLACESEL, (IntPtr)1, new IntPtr(ptr));
			if (Owner != null && previousText != GetText())
				NSAccessibility.PostNotification(this, NSAccessibilityElement.SelectedTextChangedNotification);
			else
				NotifySelectionChanged(selection);
		}

		public virtual bool AccessibilityIsSelectedTextRangeAttributeSettable
		{
			[Export("accessibilityIsSelectedTextRangeAttributeSettable")]
			get => CanSelect;
		}

		[Export("accessibilitySetSelectedTextRangeAttribute:")]
		public virtual void AccessibilitySetSelectedTextRangeAttribute(NSRange value)
		{
			AccessibilitySelectedTextRange = value;
		}

		public override NSRange AccessibilitySelectedTextRange
		{
			get => IsPassword ? new NSRange(0, 0) : ClampRange(SelectedRange, GetText().Length);
			set
			{
				if (!CanSelect)
					return;
				var selection = SelectedRange;
				SelectedRange = value;
				NotifySelectionChanged(selection);
			}
		}

		void NotifySelectionChanged(NSRange previous)
		{
			if (Owner == null)
				return;
			var current = SelectedRange;
			if (previous.Location != current.Location || previous.Length != current.Length)
				NSAccessibility.PostNotification(this, NSAccessibilityElement.SelectedTextChangedNotification);
		}

		public override nint AccessibilityInsertionPointLineNumber
		{
			get
			{
				if (IsPassword || Owner == null)
					return 0;
				return Math.Max(0, (int)Application.SendMessage(Handle, Msg.EM_LINEFROMCHAR,
					(IntPtr)SelectedRange.Location, IntPtr.Zero));
			}
			set { } // AppKit defines this information as read-only.
		}

		[Export("accessibilityFrameForRange:")]
		public virtual CGRect AccessibilityFrameForRange(NSRange range)
		{
			if (Owner is not Control owner || IsPassword)
				return CGRect.Empty;
			var text = GetText();
			range = ClampRange(range, text.Length);
			int start = (int)range.Location, end = start + (int)range.Length;
			var first = GetCharacterPosition(start);
			var rect = new Rectangle(first.X, first.Y, 0, owner.Font.Height);
			var p = first;
			for (int index = start; index < end; index++)
			{
				var q = GetCharacterPosition(index + 1);
				int width = q.Y == p.Y ? Math.Max(0, q.X - p.X) : 0;
				// The following caret gives the actual glyph advance, including zero-width
				// characters. Only a soft-wrap boundary lacks an endpoint on this line.
				if (q.Y != p.Y && text[index] != '\r' && text[index] != '\n')
					width = TextRenderer.MeasureText(text.Substring(index, 1), owner.Font,
						Size.Empty, TextFormatFlags.NoPadding).Width;
				rect = Rectangle.Union(rect, new Rectangle(p.X, p.Y, width, owner.Font.Height));
				p = q;
			}
			return driver.MonoToNativeScreen(owner.RectangleToScreen(rect));
		}

		unsafe Point GetCharacterPosition(int index)
		{
			POINT point = default;
			Application.SendMessage(Handle, Msg.EM_POSFROMCHAR, new IntPtr(&point), new IntPtr(index));
			return new Point(point.x, point.y);
		}
	}
}
