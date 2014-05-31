using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net.Tests
{
	[TestFixture]
	public class IntegrationTests : Assert
	{
		[Test, Ignore]
		public void Test()
		{
			JsEmu.EnterJsEventLoop(TestInternal);
		}

		private void TestInternal()
		{
			var @params = new PCEmulatorParams
			{
				mem_size = 1 * 1024 * 1024
			};

			const int RAM_BASE = 0x00;
			var pc = new PCEmulator(@params);
			pc.load_binary("test_add.tst.bin", RAM_BASE);
			pc.cpu.eip = RAM_BASE;

			pc.start();
		}

		[Test]
		public void TestLinuxLoading()
		{
			JsEmu.EnterJsEventLoop(TestLinuxLoadingInternal);
		}

		private void TestLinuxLoadingInternal()
		{

			var termBuffer = new StringBuilder();
			Action<char> serialWrite = x => termBuffer.Append(x);
			var pc = PCEmulatorBuilder.BuildLinuxReady(serialWrite);

			var cpu86 = (CPU_X86_Impl) pc.cpu;
			var actual = new List<string>();
			cpu86.TestLogEvent += actual.Add;

			bool err;
			var reset = pc.Cycle(out err, 57812 + 1);
			IsFalse(err);
			IsFalse(reset);
			var expected = File.ReadAllLines("log1.txt");
			var min = Math.Min(expected.Length, actual.Count);
			for(var i=0; i < min; i++)
			{
				var e = expected[i];
				var a = actual[i];
				AreEqual(e, a, string.Format("Wrong on line: {0} ({1}%)", i+1, i/min));
			}
			AreEqual(expected.Length, actual.Count, "wrong length");
			AreEqual("Starting Linux\r\n", termBuffer.ToString());
		}
	}
}