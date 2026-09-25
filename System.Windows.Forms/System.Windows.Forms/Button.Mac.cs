using System;
using System.Drawing.Mac;
using System.Windows.Forms.Mac;
using AppKit;

namespace System.Windows.Forms
{
	public partial class Button : IMacNativeControl
	{
		sealed class DialogButton : NSButton
		{
			internal bool AcceptsReturn { get; set; }

			public override bool PerformKeyEquivalent(NSEvent e)
			{
				if (AcceptsReturn && Enabled && e.Type == NSEventType.KeyDown &&
				    e.CharactersIgnoringModifiers == "\r" &&
				    (e.ModifierFlags & (NSEventModifierMask.CommandKeyMask |
				        NSEventModifierMask.ControlKeyMask | NSEventModifierMask.AlternateKeyMask)) == 0) {
					PerformClick(this);
					return true;
				}
				return base.PerformKeyEquivalent(e);
			}
		}

		DialogButton button;
		bool is_cancel_button;

		public NSView CreateView()
		{
			var i = Image;

			button = new DialogButton();
			/*button.AttributedTitle = GetAttributedString(Text, '&', b.font, b.TextAlign);
			button.Alignment = TextAlign.ToNSTextAlignment();
			cell.Highlighted = b.ButtonState == ButtonState.Pushed;
			cell.Bezeled = false;
			cell.Bordered = true;
			cell.BezelStyle = NSBezelStyle.Rounded; // When Rounded is set, the button border gets its own dimensions (smaller than we want)
			cell.Enabled = b.Enabled;
			cell.isFocused = b.Focused;*/
			button.BezelStyle = NSBezelStyle.Rounded;
			button.AttributedTitle = System.Drawing.Mac.Extensions.GetAttributedString(Text, '&', Font, TextAlign);
			button.Alignment = TextAlign.ToNSTextAlignment();
			button.Activated += (sender, e) => PerformClick();
			button.Enabled = Enabled;
			button.Image = i == null ? null : i.ToNSImage();
			button.ImagePosition = NSCellImagePosition.ImageLeft;
			UpdateKeyEquivalent();

			return button;
		}

		public override string Text
		{
			get
			{
				return base.Text;
			}
			set
			{
				base.Text = value;
				if (button != null)
					button.AttributedTitle = System.Drawing.Mac.Extensions.GetAttributedString(value, '&', Font, TextAlign);
			}
		}

		public override Drawing.Font Font
		{
			get
			{
				return base.Font;
			}
			set
			{
				base.Font = value;
				if (button != null)
					button.AttributedTitle = System.Drawing.Mac.Extensions.GetAttributedString(Text, '&', value, TextAlign);
			}
		}

		public override Drawing.ContentAlignment TextAlign
		{
			get
			{
				return base.TextAlign;
			}
			set
			{
				base.TextAlign = value;
				if (button != null)
				{
					button.AttributedTitle = System.Drawing.Mac.Extensions.GetAttributedString(Text, '&', Font, value);
					button.Alignment = value.ToNSTextAlignment();
				}
			}
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			if (button != null)
				button.Enabled = Enabled;
			base.OnEnabledChanged(e);
		}

		internal override Drawing.Size GetPreferredSizeCore(Drawing.Size proposedSize)
		{
			if (this.AutoSize && NativeButton is NSButton button)
				return button.SizeThatFits(proposedSize.ToCGSize()).ToSDSize();

			return base.GetPreferredSizeCore(proposedSize);
		}

		internal virtual NSButton? NativeButton
		{
			get
			{
				if (button == null)
					CreateView();
				return button;
			}
		}

		void UpdateKeyEquivalent()
		{
			if (button == null)
				return;
			button.KeyEquivalent = is_cancel_button ? "\u001b" : IsDefault ? "\r" : "";
			button.AcceptsReturn = is_cancel_button && IsDefault;
		}

		internal void SetCancelButton(bool value)
		{
			is_cancel_button = value;
			UpdateKeyEquivalent();
		}

		internal protected override bool IsDefault
		{
			get
			{
				return base.IsDefault;
			}
			set
			{
				base.IsDefault = value;
				UpdateKeyEquivalent();
			}
		}

		internal override void OnImageChanged()
		{
			if (button != null)
			{
				var i = Image;
				button.Image = i == null ? null : i.ToNSImage();
			}
			base.OnImageChanged();
		}
	}
}
