using System.IO;
using log4net.Config;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	internal class Program
	{
		private PCEmulator pc;

		private static void Main()
		{
			var app = new Program();
			JsEmu.EnterJsEventLoop(app.Start);
		}

		private void Start()
		{
			XmlConfigurator.ConfigureAndWatch(new FileInfo("settings.log4net.xml"));
			var term = new Term(80, 30, str => pc.serial.send_chars(str));

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
	}
}