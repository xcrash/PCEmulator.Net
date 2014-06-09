using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PCEmulator.Net.Tests.Integration
{
	public class PCEmulatorTestsBase : Assert
	{
		protected void Test(IEnumerable<string> expectedDebugLog, int? maxSteps = null)
		{
			var mockDate = new DateTime(2011, 1, 1, 2, 3, 4, 567);
			var pc = PCEmulatorBuilder.BuildLinuxReady(x => { }, mockDate);

			var i = 0;
			var actualDebugLog = GetDebugLog(pc);
			var expE = expectedDebugLog.GetEnumerator();
			var actE = actualDebugLog.GetEnumerator();

			int? prevP = null;
			for (; expE.MoveNext() && actE.MoveNext();)
			{
				try
				{
					AreEqual(expE.Current, actE.Current,
						string.Format("Wrong on line: {0}:\r\nexpected:{1}\r\nactual:{2}\r\n", i + 1, expE.Current, actE.Current));
				}
				catch (Exception e)
				{
					Fail("Fail on line: {0} with error: {1}", (i + 1), e);
				}

				var percent = 0;
				if (maxSteps.HasValue)
				{
					percent = 100*i/maxSteps.Value;

					if (!prevP.HasValue || prevP.Value != percent)
						Console.WriteLine(percent + @"%");
				}
				prevP = percent;

				i++;
				if (maxSteps.HasValue && i + 1 > maxSteps)
					break;
			}
		}

		private IEnumerable<string> GetDebugLog(PCEmulator pc)
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
					throw new Exception("Error during cycle", ex);
			}
		}
	}
}