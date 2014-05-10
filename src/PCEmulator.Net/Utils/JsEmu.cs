using System;
using System.Collections.Generic;
using System.Linq;

namespace PCEmulator.Net.Utils
{
	public static class JsEmu
	{
		private static readonly Queue<TimeoutMeta> timeouts = new Queue<TimeoutMeta>();

		private class TimeoutMeta
		{
			public Action Action { get; set; }
			public uint Timeout { get; set; }
			public DateTime Now { get; set; }
		}

		public static void SetTimeout(Action action, uint timeout = 0)
		{
			timeouts.Enqueue(new TimeoutMeta
				{
					Action = action,
					Timeout = timeout,
					Now = DateTime.Now
				});
		}

		public static void EnterJsEventLoop(Action entryPoint)
		{
			SetTimeout(entryPoint);
			TimeoutsLoop();
		}

		private static void TimeoutsLoop()
		{
			while (timeouts.Any())
			{
				var next = timeouts.Dequeue();

				if (DateTime.Now >= next.Now.AddMilliseconds(next.Timeout))
					next.Action();
				else
					timeouts.Enqueue(next);
			}
		}
	}
}