using System;
using System.Diagnostics;
using System.Drawing.Mac;
using NUnit.Framework;
using CoreGraphics;

namespace MacBridge.Test
{
	[TestFixture ()]
	public class CGImageExtensionsTest
	{
		[SetUp]
		public void Setup()
		{
			Trace.Listeners.Add (new ConsoleTraceListener (true));
		}

		[Test ()]
		public void TestCreateBitmapFromCGImage ()
		{
			// Load CGImage
			var path = System.IO.Path.Combine (TestContext.CurrentContext.TestDirectory, "data", "SetupLogo.jpg");
			Debug.WriteLine (System.IO.Path.GetFullPath (path));
			using var provider = new CGDataProvider (path);
			using var cgImage = CGImage.FromJPEG(provider, null, false, CGColorRenderingIntent.Default);

			// Convert it to System.Drawing.Bitmap using Bitmap's internal constructor
			using var bitmap = cgImage.ToBitmap ();

			Assert.That (bitmap.Width, Is.EqualTo (checked((int)cgImage.Width)));
			Assert.That (bitmap.Height, Is.EqualTo (checked((int)cgImage.Height)));
		}
	}
}

