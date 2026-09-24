using System.Windows.Forms;
//using MailClient.UI.Controls;
using System;

#if MAC

using AppKit;
using Foundation;
using WebKit;

#endif // MAC

namespace FormsTest
{
	partial class WebForm : Form
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		#if MAC 
		NSView container;
		WKWebView webView;

		#endif

		public WebForm()
		{
			InitializeComponent();
			InstallWebview();

			Load += LayoutForm_Load;
		}

		private void InitializeComponent()
		{
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				Close();
				return true;
			}
			return base.ProcessDialogKey(keyData);
		}

		protected void InstallWebview()
		{
			#if MAC

			this.container = (NSView)ObjCRuntime.Runtime.GetNSObject(this.Handle);
			this.webView = new WKWebView(container.Bounds, new WKWebViewConfiguration());
			container.AddSubview(webView);

			var url = new NSUrl("http://idnes.cz");
			webView.LoadRequest(new NSUrlRequest(url));

			#endif
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			#if MAC
			if (webView != null && container != null)
				webView.Frame = container.Bounds;
			#endif
		}

		void LayoutForm_Load(object sender, EventArgs e)
		{
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

	}
}