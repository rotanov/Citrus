﻿#if WIN
using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace Lime
{
	public class WebBrowser : Image
	{
		public Uri Url { get { return browser.Url; } set { browser.Url = value; } }

		private System.Windows.Forms.WebBrowser browser;

		static Texture2D texture = new Texture2D();

		public WebBrowser() : base(texture)
		{
			browser = new System.Windows.Forms.WebBrowser();
			browser.DocumentCompleted += browser_DocumentCompleted;
		}

		public WebBrowser(Widget parentWidget): this()
		{
			parentWidget.AddNode(this);
			Size = parentWidget.Size;
			Anchors = Anchors.LeftRightTopBottom;
			browser.Width = (int)Width;
			browser.Height = (int)Height;
			browser.ScrollBarsEnabled = false;
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			if (browser != null) {
				browser.Width = (int)Width;
				browser.Height = (int)Height;
			}
		}

		private Texture2D browserImage = new Texture2D();

		void browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			// FIXME: It's hard to make a browser to work over an OpenGL surface, so at least draw it as a picture.
			var bitmap = new System.Drawing.Bitmap((int)Width, (int)Height);
			browser.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
			var m = new MemoryStream(1024 * 1024);
			bitmap.Save(m, System.Drawing.Imaging.ImageFormat.Png);
			m.Position = 0;
			texture.LoadImage(m);
		}
		
		public override void Dispose()
		{
			if (browser != null)
				browser.Dispose();
			texture.LoadImage(new Color4[] { }, 0, 0, false);
		}
	}
}
#endif