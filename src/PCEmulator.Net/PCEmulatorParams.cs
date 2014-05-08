using System;

namespace PCEmulator.Net
{
	public class PCEmulatorParams
	{
		public object mem_size;
		public Action<char> serial_write;
		public object clipboard_get;
		public object clipboard_set;
		public object get_boot_time;
	}
}