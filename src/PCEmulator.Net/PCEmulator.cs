using System;
using System.Collections.Generic;
using System.Linq;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class PCEmulator
	{
		public readonly CPU_X86 cpu;
		private readonly PIC_Controller pic;
		private readonly PIT pit;
		private CMOS cmos;
		public Serial serial;
		private Keyboard kbd;
		private Clipboard jsclipboard;
		private readonly bool reset_request;
		private Func<uint, byte>[] ioport_readb_table;
		private Func<uint, ushort>[] ioport_readw_table;
		private Func<uint, uint>[] ioport_readl_table;
		private Action<uint, byte>[] ioport_writeb_table;
		private Action<uint, ushort>[] ioport_writew_table;
		private Action<uint, uint>[] ioport_writel_table;
		private int request_request;

		public PCEmulator(PCEmulatorParams uh, DateTime? cmosFixedDate = null)
		{
			var cpu = new CPU_X86_Impl();
			this.cpu = cpu;
			cpu.phys_mem_resize(uh.mem_size);
			init_ioports();
			register_ioport_write(0x80, 1, 1, ioport80_write);
			pic = new PIC_Controller(this, 0x20, 0xa0, cpu.set_hard_irq_wrapper);
			pit = new PIT(this, (x) => pic.set_irq(0, x), cpu.return_cycle_count);
			cmos = new CMOS(this, cmosFixedDate);
			serial = new Serial(this, 0x3f8, (x) => pic.set_irq(4, x), uh.serial_write);
			kbd = new Keyboard(this, reset);
			if (uh.get_boot_time != null)
				jsclipboard = new Clipboard(this, 0x3c0, uh.get_boot_time);
			reset_request = false;
			cpu.ld8_port = ld8_port;
			cpu.ld16_port = ld16_port;
			cpu.ld32_port = ld32_port;
			cpu.st8_port = st8_port;
			cpu.st16_port = st16_port;
			cpu.st32_port = st32_port;
			cpu.get_hard_intno = () => pic.get_hard_intno();
		}

		public uint load_binary(string url, uint mem8_loc)
		{
			return cpu.load_binary(url, mem8_loc);
		}

		public void start(uint? mCycles = null)
		{
			setTimeout(() => timer_func(10, mCycles));
		}

		public bool Cycle(out bool errOnExit, uint? mCycles = null)
		{
			var pc = this;
			var cpu = pc.cpu;
			var ncycles = mCycles.HasValue ? cpu.cycle_count + mCycles.Value : cpu.cycle_count + 100000;

			var doReset = false;
			errOnExit = false;

			while (cpu.cycle_count < ncycles)
			{
				pc.pit.update_irq();
				var exitStatus = cpu.exec(ncycles - cpu.cycle_count);
				if (exitStatus == 256)
				{
					if (!pc.reset_request)
						continue;
					doReset = true;
					break;
				}
				if (exitStatus == 257)
				{
					errOnExit = true;
					break;
				}

				doReset = true;
				break;
			}
			return doReset;
		}

		public uint[] LoadBinnaries(Dictionary<string, uint> memMap)
		{
			return memMap.Select(x => load_binary(x.Key, x.Value)).ToArray();
		}

		private void timer_func(int i, uint? mCycles = null)
		{
			bool errOnExit;
			var doReset = Cycle(out errOnExit, mCycles);
			
			if (doReset)
				return;
			if (errOnExit)
			{
				setTimeout(() => timer_func(10, mCycles));
			}
			else
			{
				setTimeout(() => timer_func(0, mCycles));
			}
		}

		private void init_ioports()
		{
			this.ioport_readb_table = new Func<uint, byte>[1024];
			this.ioport_readw_table = new Func<uint, ushort>[1024];
			this.ioport_readl_table = new Func<uint, uint>[1024];
			this.ioport_writeb_table = new Action<uint, byte>[1024];
			this.ioport_writew_table = new Action<uint, ushort>[1024];
			this.ioport_writel_table = new Action<uint, uint>[1024];
			Func<uint, ushort> readw = default_ioport_readw;
			Action<uint, ushort> writew = default_ioport_writew;
			for (var i = 0; i < 1024; i++)
			{
				this.ioport_readb_table[i] = this.default_ioport_readb;
				this.ioport_readw_table[i] = readw;
				this.ioport_readl_table[i] = this.default_ioport_readl;
				this.ioport_writeb_table[i] = this.default_ioport_writeb;
				this.ioport_writew_table[i] = writew;
				this.ioport_writel_table[i] = this.default_ioport_writel;
			}
		}

		private byte default_ioport_readb(uint port_num)
		{
			const int x = 0xff;
			return x;
		}

		private ushort default_ioport_readw(uint port_num)
		{
			ushort x = this.ioport_readb_table[port_num](port_num);
			port_num = (port_num + 1) & (1024 - 1);
			x |= (ushort)(this.ioport_readb_table[port_num](port_num) << 8);
			return x;
		}

		private uint default_ioport_readl(uint port_num)
		{
			const uint x = 0xffff;
			return x;
		}

		private void default_ioport_writeb(uint port_num, byte x)
		{
		}

		private void default_ioport_writew(uint port_num, ushort x)
		{
			this.ioport_writeb_table[port_num](port_num, (byte) (x & 0xff));
			port_num = (port_num + 1) & (1024 - 1);
			this.ioport_writeb_table[port_num](port_num, (byte) ((x >> 8) & 0xff));
		}

		private void default_ioport_writel(uint port_num, uint x)
		{
		}

		private byte ld8_port(uint port_num)
		{
			var x = this.ioport_readb_table[port_num & (1024 - 1)](port_num);
			return x;
		}

		private ushort ld16_port(uint port_num)
		{
			var x = this.ioport_readw_table[port_num & (1024 - 1)](port_num);
			return x;
		}

		private uint ld32_port(uint port_num)
		{
			var x = this.ioport_readl_table[port_num & (1024 - 1)](port_num);
			return x;
		}

		private void st8_port(uint port_num, byte x)
		{
			this.ioport_writeb_table[port_num & (1024 - 1)](port_num, x);
		}

		private void st16_port(uint port_num, ushort x)
		{
			this.ioport_writew_table[port_num & (1024 - 1)](port_num, x);
		}

		private void st32_port(uint port_num, uint x)
		{
			this.ioport_writel_table[port_num & (1024 - 1)](port_num, x);
		}

		public void register_ioport_read(int start, int len, int iotype, Func<uint, byte> io_callback)
		{
			switch (iotype)
			{
				case 1:
					for (var i = start; i < start + len; i++)
					{
						ioport_readb_table[i] = io_callback;
					}
					break;
			}
		}

		private void register_ioport_read(int start, int len, int iotype, Func<uint, ushort> io_callback)
		{
			switch (iotype)
			{
				case 2:
					for (var i = start; i < start + len; i += 2)
					{
						ioport_readw_table[i] = io_callback;
					}
					break;
			}
		}

		public void register_ioport_read(int start, int len, int iotype, Func<uint, uint> io_callback)
		{
			switch (iotype)
			{
				case 4:
					for (var i = start; i < start + len; i += 4)
					{
						ioport_readl_table[i] = io_callback;
					}
					break;
			}
		}

		public void register_ioport_write(int start, int len, int iotype, Action<uint, byte> io_callback)
		{
			switch (iotype)
			{
				case 1:
					for (var i = start; i < start + len; i++)
					{
						ioport_writeb_table[i] = io_callback;
					}
					break;
			}
		}

		private void register_ioport_write(int start, int len, int iotype, Action<uint, ushort> io_callback)
		{
			switch (iotype)
			{
				case 2:
					for (var i = start; i < start + len; i += 2)
					{
						ioport_writew_table[i] = io_callback;
					}
					break;
			}
		}

		private void register_ioport_write(int start, int len, int iotype, Action<uint, uint> io_callback)
		{
			switch (iotype)
			{
				case 4:
					for (var i = start; i < start + len; i += 4)
					{
						ioport_writel_table[i] = io_callback;
					}
					break;
			}
		}

		private void ioport80_write(uint mem8_loc, byte data)
		{
			
		}

		private void reset()
		{
			this.request_request = 1;
		}

		void setTimeout(Action action, uint timeout = 0)
		{
			JsEmu.SetTimeout(action, timeout);
		}
	}
}