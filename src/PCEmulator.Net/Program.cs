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
		private readonly static Buffer<string> traceBuffer = new Buffer<string>(15);

		private static void Main(string[] args)
		{
			var app = new Program();
			JsEmu.EnterJsEventLoop(app.Start);
		}

		private void Start()
		{
			XmlConfigurator.ConfigureAndWatch(new FileInfo("settings.log4net.xml"));

			var term = new Term(80, 30, str => pc.serial.send_chars(str));
// ReSharper disable once RedundantAssignment
			var isTraceEnabled = false;
#if TRACE_LOG
			isTraceEnabled = true;
#endif
			pc = PCEmulatorBuilder.BuildLinuxReady(term.Write, null, getBootTime, isTraceEnabled);
			if (isTraceEnabled)
			{
				((CPU_X86_Impl)pc.cpu).TestLogEvent += traceBuffer.Add;
			}

			boot_start_time = DateTime.Now;
			pc.start();
		}

		private int getBootTime()
		{
			return (int) (DateTime.Now - boot_start_time).TotalMilliseconds;
		}
	}
}
