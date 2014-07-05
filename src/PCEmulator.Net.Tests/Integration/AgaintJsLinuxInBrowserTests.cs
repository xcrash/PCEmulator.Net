using System.Text;
using NUnit.Framework;
using PCEmulator.Net.Tests.Utils;

namespace PCEmulator.Net.Tests.Integration
{
	[TestFixture]
	public class AgaintJsLinuxInBrowserTests : PCEmulatorTestsBase
	{
		[Test]
		public void TestLinuxLoading()
		{
			var termBuffer = new StringBuilder();
			using (var traceLogs = GetTraceLogFromJsLinux())
			{
				TestAgainstTraceLog(traceLogs, 60000, x => termBuffer.Append(x));
				AreEqual("Starting Linux\r\n", termBuffer.ToString());
			}
		}

		[Test]
		public void TestUntilCommandLineAvailable()
		{
			using (var traceLog = GetTraceLogFromJsLinux())
			{
				TestAgainstTraceLog(traceLog, 20000000);
			}
		}

		private static IEnumerableDisposable<string> GetTraceLogFromJsLinux()
		{
			return new BrowserDebugLogEnumeratorBuilder().Build();
		}
	}
}