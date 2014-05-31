using System;
using System.Collections.Generic;
using System.IO;
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

		[Test]
		public void TestLinuxLoading()
		{
			JsEmu.EnterJsEventLoop(TestLinuxLoadingInternal);
		}

		private void TestLinuxLoadingInternal()
		{

			Action<char> nullTerminal = x => { };
			var @params = new PCEmulatorParams
			{
				serial_write = nullTerminal,
				mem_size = 16 * 1024 * 1024
			};

			var pc = new PCEmulator(@params);

			pc.load_binary("vmlinux-2.6.20.bin", 0x00100000);

			var initrdSize = pc.load_binary("root.bin", 0x00400000);

			const int startAddr = 0x10000;
			pc.load_binary("linuxstart.bin", startAddr);

			const int cmdlineAddr = 0xf800;
			pc.cpu.write_string(cmdlineAddr, "console=ttyS0 root=/dev/ram0 rw init=/sbin/init notsc=1");

			pc.cpu.eip = startAddr;
			pc.cpu.regs[CPU_X86.REG_EAX] = @params.mem_size; /* eax */
			pc.cpu.regs[CPU_X86.REG_EBX] = initrdSize; /* ebx */
			pc.cpu.regs[CPU_X86.REG_ECX] = cmdlineAddr; /* ecx */

			var cpu86 = (CPU_X86_Impl) pc.cpu;
			var actual = new List<string>();
			cpu86.TestLogEvent += actual.Add;

			bool err;
			var reset = pc.Cycle(out err, 57812 + 1);
			IsFalse(err);
			IsFalse(reset);
			var expected = File.ReadAllLines("log1.txt");
			var min = Math.Min(expected.Length, actual.Count);
			for(var i=0; i < min; i++)
			{
				var e = expected[i];
				var a = actual[i];
				AreEqual(e, a, string.Format("Wrong on line: {0} ({1}%)", i+1, i/min));
			}
			AreEqual(expected.Length, actual.Count, "wrong length");
		}
	}
}