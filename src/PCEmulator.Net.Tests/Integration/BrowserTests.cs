using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.ServiceModel;
using NUnit.Framework;
using PCEmulator.Js.Wrapper;

namespace PCEmulator.Net.Tests.Integration
{
	[TestFixture]
	public class BrowserTests : PCEmulatorTestsBase
	{
		[Test]
		public void TestVsBrowser()
		{
			try
			{
				Test(GetDebugLogFromBrowser(), 1128189);
			}
			finally
			{
				BrowserDebugLogEnumeratorBuilder.KillWrapper();
			}
		}

		private IEnumerable<string> GetDebugLogFromBrowser()
		{
			return new BrowserDebugLogEnumeratorBuilder().Build();
		}

		internal class BrowserDebugLogEnumeratorBuilder
		{
			private ServiceHost host;
			private static int? wrapperProcId;

			internal IEnumerable<string> Build()
			{
				host = new ServiceHost(typeof(JsHostHandler), new Uri("net.pipe://localhost"));
				{
					host.AddServiceEndpoint(typeof(IJsHostHandler),
						new NetNamedPipeBinding
						{
							MaxReceivedMessageSize = 104857600
						},
						"PipeReverse");

					host.Open();

					return Observable.FromEvent<string>(Subscribe, UnSubscribe)
						.SelectMany(x => x.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
						.Select(x => x.Replace("undefined", "0")) //TODO: it's only solution for the First debugLine
						.Select(x => x.TrimEnd())
						.ToEnumerable();
				}
			}

			private static void Subscribe(Action<string> s)
			{
				JsHostHandler.DebugLogEvent += s;

				const string EXE = @"PCEmulator.Js.Wrapper.exe";
				var proc = Process.Start(EXE);
				if (proc == null)
					throw new Exception("Can't start " + EXE);
				wrapperProcId = proc.Id;
			}

			private static void UnSubscribe(Action<string> s)
			{
				JsHostHandler.DebugLogEvent -= s;
				KillWrapper();
			}

			internal static void KillWrapper()
			{
				if (!wrapperProcId.HasValue) return;
				Process.GetProcessById(wrapperProcId.Value).Kill();
				wrapperProcId = null;
			}

			public class JsHostHandler : IJsHostHandler
			{
				public static event Action<string> DebugLogEvent;

				public void DebugLog(string log)
				{
					OnDebugLogEvent(log);
				}

				public void Ping()
				{
				}

				private static void OnDebugLogEvent(string obj)
				{
					var handler = DebugLogEvent;
					if (handler != null)
						handler(obj);
				}
			}
		}
	}
}