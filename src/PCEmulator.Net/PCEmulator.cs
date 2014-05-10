using System;
using System.Collections.Generic;
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
		private readonly bool reset_request;
		private Func<uint, byte>[] ioport_readb_table;
		private Func<uint, ushort>[] ioport_readw_table;
		private Func<uint, uint>[] ioport_readl_table;
		private Action<uint, byte>[] ioport_writeb_table;
		private Action<uint, ushort>[] ioport_writew_table;
		private Action<uint, uint>[] ioport_writel_table;
		private int request_request;

		public PCEmulator(PCEmulatorParams uh)
		{
			var cpu = new CPU_X86_Impl();
			this.cpu = cpu;
			cpu.phys_mem_resize(uh.mem_size);
			init_ioports();
			register_ioport_write(0x80, 1, 1, ioport80_write);
			pic = new PIC_Controller(this, 0x20, 0xa0, cpu.set_hard_irq_wrapper);
			pit = new PIT(this, (x) => pic.set_irq(0, x), cpu.return_cycle_count);
			cmos = new CMOS(this);
			serial = new Serial(this, 0x3f8, (x) => pic.set_irq(4, x), uh.serial_write);
			kbd = new Keyboard(this, reset);
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

		public void start()
		{
			setTimeout(() => timer_func(10));
		}

		private void timer_func(int i)
		{
			bool do_reset;
			bool err_on_exit;
			var PC = this;
			var cpu = PC.cpu;
			var Ncycles = cpu.cycle_count + 100000;

			do_reset = false;
			err_on_exit = false;

			while (cpu.cycle_count < Ncycles)
			{
				PC.pit.update_irq();
				var exit_status = cpu.exec(Ncycles - cpu.cycle_count);
				if (exit_status == 256)
				{
					if (PC.reset_request)
					{
						do_reset = true;
						break;
					}
				}
				else if (exit_status == 257)
				{
					err_on_exit = true;
					break;
				}
				else
				{
					do_reset = true;
					break;
				}
			}
			if (!do_reset)
			{
				if (err_on_exit)
				{
					setTimeout(() => timer_func(10));
				}
				else
				{
					setTimeout(() => timer_func(0));
				}
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

		private void register_ioport_read(int start, int len, int iotype, Func<uint, uint> io_callback)
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