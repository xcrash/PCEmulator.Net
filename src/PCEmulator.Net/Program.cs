namespace PCEmulator.Net
{
	internal class Program
	{
		private static Term term;
		private static PCEmulator pc;

		private static void term_start()
		{
			term = new Term(80, 30, term_handler);
			term.open();
		}

		/// <summary>
		/// send chars to the serial port
		/// </summary>
		private static void term_handler(string str)
		{
			pc.serial.send_chars(str);
		}

		private static void Main(string[] args)
		{
			term_start();

			object start_addr;
			object initrd_size;
			PCEmulatorParams @params;
			object cmdline_addr;

			@params = new PCEmulatorParams();

			//serial output chars
			@params.serial_write = () => term.write();

			//memory size (in bytes)
			@params.mem_size = 16*1024*1024;

			pc = new PCEmulator(@params);

			pc.load_binary("vmlinux-2.6.20.bin", 0x00100000);

			initrd_size = pc.load_binary("root.bin", 0x00400000);

			start_addr = 0x10000;
			pc.load_binary("linuxstart.bin", start_addr);

			//set the Linux kernel command line
			//Note: we don't use initramfs because it is not possible to
			//disable gzip decompression in this case, which would be too
			//slow.
			cmdline_addr = 0xf800;
			pc.cpu.write_string(cmdline_addr, "console=ttyS0 root=/dev/ram0 rw init=/sbin/init notsc=1");

			pc.cpu.eip = start_addr;
			pc.cpu.regs[0] = @params.mem_size; /* eax */
			pc.cpu.regs[3] = initrd_size; /* ebx */
			pc.cpu.regs[1] = cmdline_addr; /* ecx */

			pc.start();
		}
	}
}