using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			var termBuffer = new StringBuilder();
			var actual = new List<string>();
			var pc = PCEmulatorBuilder.BuildLinuxReady(x => termBuffer.Append(x));
			((CPU_X86_Impl) pc.cpu).TestLogEvent += actual.Add;

			bool err;
			var reset = pc.Cycle(out err, 57812 + 1);
			IsFalse(err);
			IsFalse(reset);
			var expected = File.ReadAllLines("log0.txt");
			var min = Math.Min(expected.Length, actual.Count);
			for(var i=0; i < min; i++)
			{
				var e = expected[i];
				var a = actual[i];
				AreEqual(e, a, string.Format("Wrong on line: {0} ({1}%)", i + 1, (i + 1) / (min + 1)));
			}
			AreEqual(expected.Length, actual.Count, "wrong length");
			AreEqual("Starting Linux\r\n", termBuffer.ToString());
		}

		[Test]
		public void InfinitiveTest()
		{
			
			var pc = PCEmulatorBuilder.BuildLinuxReady(x => { });

			var i = 0;
			var expectedDebugLog = GetAllDebugLog();
			var actualDebugLog = GetAllDebugLog(pc);
			var expE = expectedDebugLog.GetEnumerator();
			var actE = actualDebugLog.GetEnumerator();

			for (; expE.MoveNext() && actE.MoveNext(); )
			{
				try
				{
					AreEqual(expE.Current, actE.Current, string.Format("Wrong on line: {0}:\r\nexpected:{1}\r\nactual:{2}\r\n", i + 1, expE.Current, actE.Current));
				}
				catch (Exception e)
				{
					Fail("Fail on line: {0} with error: {1}", (i + 1), e);
				}

				i++;
			}
		}

		private IEnumerable GetAllDebugLog(PCEmulator pc)
		{
			var actual = new List<string>();
			((CPU_X86_Impl)pc.cpu).TestLogEvent += actual.Add;
			while (true)
			{
				Exception ex = null;
				try
				{
					bool err;
					pc.Cycle(out err, 100000);
				}
				catch (Exception e)
				{
					
					ex = e;
				}


				foreach (var s in actual)
				{
					yield return s;
				}
				actual.Clear();

				if (ex != null)
					throw ex;
			}
		}

		private IEnumerable GetAllDebugLog()
		{
			return Enumerable.Range(0, 10)
				.Select(x => "log" + x + ".txt")
				.SelectMany(File.ReadAllLines);
		}
	}
}