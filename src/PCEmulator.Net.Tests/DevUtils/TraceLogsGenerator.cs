using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using PCEmulator.Net.Tests.Integration;

namespace PCEmulator.Net.Tests.DevUtils
{
	[TestFixture]
	public class TraceLogsGenerator : PCEmulatorTestsBase
	{
		private const int LOG_FILES_TO_CREATE = 22;

		[Test, Ignore("Manual functionality exists as UnitTest")]
		public void GenerateFromJsLinux()
		{
			using (var traceLogs = new BrowserDebugLogEnumeratorBuilder().Build().GetEnumerator())
			{
				Generate(traceLogs);
			}
		}

		[Test, Ignore("Manual functionality exists as UnitTest")]
		public void GenerateFromSelf()
		{
			var mockDate = new DateTime(2011, 1, 1, 2, 3, 4, 567);
			var pc = PCEmulatorBuilder.BuildLinuxReady(x => { },  mockDate);
			var traceLogs = GetDebugLog(pc).GetEnumerator();

			Generate(traceLogs);
		}

		private static void Generate(IEnumerator<string> traceLogs)
		{
			var debugLog = new StringBuilder();
			var debugFlushCount = 0;
			while (traceLogs.MoveNext())
			{
				debugLog.AppendLine(traceLogs.Current);

				if (debugLog.Length <= 7500000)
					continue;

				File.WriteAllText("traceLogs/log" + debugFlushCount + ".txt", debugLog.ToString());
				if (debugFlushCount == LOG_FILES_TO_CREATE)
					break;

				debugLog.Clear();
				debugFlushCount++;
			}
		}
	}
}