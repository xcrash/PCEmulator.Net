using System;
using System.Collections;
using System.Collections.Generic;
using PCEmulator.Net.Tests.Integration;

namespace PCEmulator.Net.Tests.Utils
{
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
}