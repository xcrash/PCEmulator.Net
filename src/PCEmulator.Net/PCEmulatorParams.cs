using System;

namespace PCEmulator.Net
{
	public class PCEmulatorParams
	{
		public uint mem_size;
		public Action<char> serial_write;
		public object get_boot_time;
	}
}