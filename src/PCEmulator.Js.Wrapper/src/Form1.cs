using System;
using System.ServiceModel;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace PCEmulator.Js.Wrapper
{
	public partial class Form1 : Form
	{
		public static Form1 Instance;

		private readonly WebView webView;

		public Form1()
		{
			Instance = this;
			InitializeComponent();

			var pipeFactory = new ChannelFactory<IJsHostHandler>(new NetNamedPipeBinding(),
				new EndpointAddress("net.pipe://localhost/PipeReverse"));

			var pipeProxy = pipeFactory.CreateChannel();
			try
			{
				pipeProxy.Ping();
			}
			catch (EndpointNotFoundException)
			{
				// ReSharper disable once SuspiciousTypeConversion.Global
				var disposable = pipeProxy as IDisposable;
				if (disposable != null)
					disposable.Dispose();
				pipeProxy = null;
			}

			var settings = new Settings();
			if (!CEF.Initialize(settings))
				throw new Exception("Can't initialize CEF");

			webView = new WebView("http://localhost/jslinux/Test3/", new BrowserSettings()) {Dock = DockStyle.Fill};
			webView.RegisterJsObject("__DOT_NET_HOST", new JsDebugLogReceiver(pipeProxy, textBox1));
			splitContainer1.Panel1.Controls.Add(webView);

			webView.ConsoleMessage += (sender, args) => Show1(args);
		}

		private static DialogResult Show1(ConsoleMessageEventArgs args)
		{
			return MessageBox.Show(string.Format("ConsoleMessage: {0}", args.Message));
		}

		private void button1_Click(object sender, EventArgs e)
		{
			webView.ShowDevTools();
		}
	}
}