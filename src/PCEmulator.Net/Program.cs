using System;

namespace PCEmulator.Net
{
	internal class Program
	{
		private static PCEmulator pc;

		// send chars to the serial port
		private static void term_handler(char str)
		{
			pc.serial.send_chars(str);
		}

		private static void Main()
		{
			TestTerm();

			var app = new Program();
			app.Start();
		}

		private void Start()
		{
			var term = new Term(80, 30, term_handler);

			var @params = new PCEmulatorParams
			{
				serial_write = term.Write,
				mem_size = 16*1024*1024
			};

			pc = new PCEmulator(@params);

			pc.load_binary("vmlinux-2.6.20.bin", 0x00100000);

			var initrdSize = pc.load_binary("root.bin", 0x00400000);

			const int startAddr = 0x10000;
			pc.load_binary("linuxstart.bin", startAddr);

			//set the Linux kernel command line
			//Note: we don't use initramfs because it is not possible to
			//disable gzip decompression in this case, which would be too
			//slow.
			const int cmdlineAddr = 0xf800;
			pc.cpu.write_string(cmdlineAddr, "console=ttyS0 root=/dev/ram0 rw init=/sbin/init notsc=1");

			pc.cpu.eip = startAddr;
			pc.cpu.regs[0] = @params.mem_size; /* eax */
			pc.cpu.regs[3] = initrdSize; /* ebx */
			pc.cpu.regs[1] = cmdlineAddr; /* ecx */

			pc.start();
		}

		private static void TestTerm()
		{
			var term = new Term(80, 30, x => { });
			while (true)
			{
				var tmp = Console.ReadKey(true).KeyChar;
				term.Write(tmp);
			}
		}
	}
}