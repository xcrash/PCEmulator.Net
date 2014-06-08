using System;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace PCEmulator.Js.Wrapper
{
	public partial class Form1 : Form
	{
		private readonly WebView webView;

		public Form1()
		{
			InitializeComponent();

			var settings = new Settings();
			if (!CEF.Initialize(settings))
				throw new Exception("Can't initialize CEF");

			webView = new WebView("http://localhost/jslinux/Test3/", new BrowserSettings()) {Dock = DockStyle.Fill};
			webView.RegisterJsObject("logReceiver", new LogReceiver(textBox1));
			splitContainer1.Panel1.Controls.Add(webView);


			webView.ConsoleMessage += (sender, args) => MessageBox.Show(args.Message);
		}
	}

	public class LogReceiver
	{
		private readonly TextBox textBox;

		public LogReceiver(TextBox textBox)
		{
			this.textBox = textBox;
		}

		// ReSharper disable once InconsistentNaming
		public void onDebugLog(string log)
		{
			if (textBox.InvokeRequired)
				textBox.Invoke(new Action(() => onDebugLog(log)));
			else
			{
				textBox.AppendText(log + Environment.NewLine);

				const int max = 256;
				if (textBox.Text.Length > max)
					textBox.Text = textBox.Text.Substring(textBox.Text.Length - max);

			}
		}
	}
}