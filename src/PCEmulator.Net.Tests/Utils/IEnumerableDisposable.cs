using System;
using System.Collections.Generic;

namespace PCEmulator.Net.Tests.Utils
{
	internal interface IEnumerableDisposable<out T> : IEnumerable<T>, IDisposable
	{
		
	}
}