using System;
using System.IO;
using log4net.Config;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	internal class Program
	{
		private PCEmulator pc;
		private DateTime boot_start_time;

		private static void Main()
		{
			var app = new Program();
			JsEmu.EnterJsEventLoop(app.Start);
		}

		private void Start()
		{
			XmlConfigurator.ConfigureAndWatch(new FileInfo("settings.log4net.xml"));

			var term = new Term(80, 30, str => pc.serial.send_chars(str));
			pc = PCEmulatorBuilder.BuildLinuxReady(term.Write, null, getBootTime);

			boot_start_time = DateTime.Now;
			pc.start();
		}

		private int getBootTime()
		{
			return (int) (DateTime.Now - boot_start_time).TotalMilliseconds;
		}
	}
}
