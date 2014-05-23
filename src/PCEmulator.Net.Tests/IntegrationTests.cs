using NUnit.Framework;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net.Tests
{
	[TestFixture]
	public class IntegrationTests : Assert
	{
		[Test, Ignore]
		public void Test()
		{
			JsEmu.EnterJsEventLoop(TestInternal);
		}

		private void TestInternal()
		{
			var @params = new PCEmulatorParams
			{
				mem_size = 1 * 1024 * 1024
			};

			const int RAM_BASE = 0x00;
			var pc = new PCEmulator(@params);
			pc.load_binary("test_add.tst.bin", RAM_BASE);
			pc.cpu.eip = RAM_BASE;

			pc.start();
		}
	}
}