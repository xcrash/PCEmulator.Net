using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace PCEmulator.Net.Tests.Integration
{
	[TestFixture]
	public class AgainstTraceLogsTests : PCEmulatorTestsBase
	{
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
			var expected = File.ReadAllLines(TracePath("log0.txt"));
			var min = Math.Min(expected.Length, actual.Count);
			for(var i=0; i < min; i++)
			{
				var e = expected[i];
				var a = actual[i];
				AreEqual(e, a, string.Format("Wrong on line: {0} ({1}%)", i + 1, (i + 1) / (min + 1)));
			}
			AreEqual("Starting Linux\r\n", termBuffer.ToString());
		}

		[Test]
		public void TestUntilCommandLineAvailable()
		{
			TestAgainstTraceLog(GetDebugLogFromTraceLogs());
		}

		private IEnumerable<string> GetDebugLogFromTraceLogs()
		{
			return Enumerable.Range(0, 23)
				.Select(x => "log" + x + ".txt")
				.Select(TracePath)
				.SelectMany(File.ReadAllLines);
		}

		private static string TracePath(string filename)
		{
			return Path.Combine("traceLogs/", filename);
		}
	}
}