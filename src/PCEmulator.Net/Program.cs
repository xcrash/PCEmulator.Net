using System.IO;
using log4net.Config;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	internal class Program
	{
		private PCEmulator pc;

		private static void Main()
		{
			var app = new Program();
			JsEmu.EnterJsEventLoop(app.Start);
		}

		private void Start()
		{
			XmlConfigurator.ConfigureAndWatch(new FileInfo("settings.log4net.xml"));

			var term = new Term(80, 30, str => pc.serial.send_chars(str));
			pc = PCEmulatorBuilder.BuildLinuxReady(term.Write);
			pc.start();
		}
	}
}