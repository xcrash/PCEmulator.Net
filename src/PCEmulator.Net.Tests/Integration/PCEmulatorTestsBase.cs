using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net.Tests.Integration
{
	public class PCEmulatorTestsBase : Assert
	{
		protected void TestAgainstTraceLog(IEnumerable<string> expectedTraceLog, int? maxSteps = null, Action<char> terminalWriteFunc = null)
		{
			var mockDate = new DateTime(2011, 1, 1, 2, 3, 4, 567);
			if(terminalWriteFunc == null)
				terminalWriteFunc = x => { };

			var pc = PCEmulatorBuilder.BuildLinuxReady(terminalWriteFunc,  mockDate);

			var i = 0;
			var actualDebugLog = GetDebugLog(pc);
			var expE = expectedTraceLog.GetEnumerator();
			var actE = actualDebugLog.GetEnumerator();

			int? prevP = null;
			var expectedBuffer = new Buffer<string>(10);
			var actualBuffer = new Buffer<string>(10);
			for (; expE.MoveNext() && actE.MoveNext();)
			{
				var lineNo = i + 1;
				try
				{
					expectedBuffer.Add(expE.Current);
					actualBuffer.Add(actE.Current);
					var message = new StringBuilder();
					if (!object.Equals(expE.Current, actE.Current))
					{
						message.AppendLine(string.Format("Wrong on line: {0}:", lineNo));
						message.AppendLine("expected:");
						message.AppendLine(expE.Current);
						message.AppendLine("actual:");
						message.AppendLine(actE.Current);
						message.AppendLine();
						message.AppendLine("expected:");
						message.AppendLine(string.Join(Environment.NewLine, expectedBuffer.ToArray()));
						message.AppendLine("actual:");
						message.AppendLine(string.Join(Environment.NewLine, actualBuffer.ToArray()));

					}
					AreEqual(expE.Current, actE.Current, message.ToString());
				}
				catch (AssertionException)
				{
					throw;
				}
				catch (Exception e)
				{
					Fail("Fail on line: {0} with error: {1}", lineNo, e);
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
				if (maxSteps.HasValue && i+1 > maxSteps)
					break;
			}
		}

		protected IEnumerable<string> GetDebugLog(PCEmulator pc)
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