using System;
using System.ServiceModel;
using System.Text;
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

			var pipeFactory = new ChannelFactory<IServer>(new NetNamedPipeBinding(),
				new EndpointAddress("net.pipe://localhost/PipeReverse"));

			var pipeProxy = pipeFactory.CreateChannel();

			var settings = new Settings();
			if (!CEF.Initialize(settings))
				throw new Exception("Can't initialize CEF");

			webView = new WebView("http://localhost/jslinux/Test3/", new BrowserSettings()) {Dock = DockStyle.Fill};
			webView.RegisterJsObject("logReceiver", new LogReceiver(pipeProxy, textBox1));
			splitContainer1.Panel1.Controls.Add(webView);

			webView.ConsoleMessage += (sender, args) => MessageBox.Show(string.Format("ConsoleMessage: {0}", args.Message));
		}

		private void button1_Click(object sender, EventArgs e)
		{
			webView.ShowDevTools();
		}
	}

	[ServiceContract]
	public interface IServer
	{
		[OperationContract]
		void DebugLog(string log);
	}

	public class LogReceiver
	{
		private readonly IServer server;
		private readonly TextBox textBox;

		public LogReceiver(TextBox textBox)
		{
			this.textBox = textBox;
		}

		public LogReceiver(IServer server)
		{
			this.server = server;
		}

		public LogReceiver(IServer server, TextBox textBox)
		{
			this.textBox = textBox;
			this.server = server;
		}

		// ReSharper disable once InconsistentNaming
		public void onDebugLog(string log)
		{
			if (textBox != null)
			{
				if (textBox.InvokeRequired)
					textBox.Invoke(new Action(() => AddDebugLog(log)));
				else
				{
					AddDebugLog(log);
				}
			}

			if (server != null)
				server.DebugLog(log);
		}

		private void AddDebugLog(string log)
		{
			var newText = new StringBuilder(textBox.Text);
			newText.AppendLine(log);

			const int max = 512;
			if (newText.Length > max)
				newText.Remove(0, newText.Length - max);

			textBox.Text = newText.ToString();
		}
	}
}