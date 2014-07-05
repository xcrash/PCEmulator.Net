using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.ServiceModel;
using PCEmulator.Js.Wrapper;

namespace PCEmulator.Net.Tests.Integration
{
	internal class BrowserDebugLogEnumeratorBuilder
	{
		private ServiceHost host;
		private static int? wrapperProcId;

		internal IEnumerableDisposable<string> Build()
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

				return new EnumerableDisposable(Observable.FromEvent<string>(Subscribe, UnSubscribe)
					.SelectMany(x => x.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
					.Select(x => x.Replace("undefined", "0"))
					.Select(x => x.TrimEnd())
					.ToEnumerable(), Close);
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

		private void UnSubscribe(Action<string> s)
		{
			JsHostHandler.DebugLogEvent -= s;
			Close();
		}

		private void Close()
		{
			host.Close();
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

	internal class EnumerableDisposable : IEnumerableDisposable<string>
	{
		private readonly IEnumerable<string> inner;
		private readonly Action disposeAction;

		public EnumerableDisposable(IEnumerable<string> inner, Action disposeAction)
		{
			this.inner = inner;
			this.disposeAction = disposeAction;
		}

		public IEnumerator<string> GetEnumerator()
		{
			return inner.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) inner).GetEnumerator();
		}

		public void Dispose()
		{
			disposeAction();
		}
	}

	internal interface IEnumerableDisposable<out T> : IEnumerable<T>, IDisposable
	{
		
	}
}