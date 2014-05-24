using NUnit.Framework;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net.Tests
{
	[TestFixture]
	public class BufferedArrayTests : Assert
	{
		[Test]
		public void ShouldCorrectPackInt32()
		{
			var new_mem_size = 4;
			var phys_mem = new byte[new_mem_size];;
			var phys_mem32 = new Int32Array(phys_mem, 0, (uint) (new_mem_size / 4));
			phys_mem32[0] = 0x0ffff00;
			AreEqual(0, phys_mem[0]);
			AreEqual(255, phys_mem[1]);
			AreEqual(255, phys_mem[2]);
			AreEqual(0, phys_mem[3]);
		}

		[Test]
		public void ShouldStoreAndRetriveEqualValue()
		{
			var mem = new Int32Array(new byte[8], 0, 2);
			mem[0] = 16777216;
			mem[1] = 16777216;
			AreEqual(16777216, mem[0]);
			AreEqual(16777216, mem[1]);
		}
	}
}