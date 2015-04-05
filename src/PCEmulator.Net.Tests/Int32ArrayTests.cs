using System;
using System.Collections;
using NUnit.Framework;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net.Tests
{
	[TestFixture]
	public class Int32ArrayTests : Assert
	{
		protected IEnumerable Impls()
		{
			yield return (Func<byte[], BufferedArray<int>>)(buffer => new Int32Array(buffer, 0, (uint) buffer.Length));
			yield return (Func<byte[], BufferedArray<int>>)(buffer => new Int32ArrayUnsafe(buffer, 0, (uint)buffer.Length));
		}

		[Test]
		public void ShouldReadCorrectOrder([ValueSource("Impls")] Func<byte[], BufferedArray<int>> factory)
		{
			var buffer = new byte[4];
			buffer[0] = 1;
			buffer[1] = 2;
			buffer[2] = 3;
			buffer[3] = 4;
			var mem = factory(buffer);
			AreEqual(67305985, mem[0]);
		}

		[Test]
		public void ShouldStoreCorrectOrder([ValueSource("Impls")] Func<byte[], BufferedArray<int>> factory)
		{
			var buffer = new byte[4];
			var mem = factory(buffer);
			mem[0] = 67305985;

			AreEqual(1, buffer[0]);
			AreEqual(2, buffer[1]);
			AreEqual(3, buffer[2]);
			AreEqual(4, buffer[3]);
		}
	}
}