using System;

namespace PCEmulator.Net
{
	public class PCEmulator
	{
		private CPU_X86 cpu;
		private readonly PIC_Controller pic;
		private object ioport80_write;
		private PIT pit;
		private CMOS cmos;
		private Serial serial;
		private KBD kbd;
		private int reset_request;
		private clipboard_device jsclipboard;

		public PCEmulator(PCEmulatorParams uh)
		{
			var cpu = new CPU_X86();
			this.cpu = cpu;
			cpu.phys_mem_resize(uh.mem_size);
			init_ioports();
			register_ioport_write(0x80, 1, 1, ioport80_write);
			pic = new PIC_Controller(this, 0x20, 0xa0, cpu.set_hard_irq_wrapper);
			pit = new PIT(this, () => pic.set_irq(0), cpu.return_cycle_count);
			cmos = new CMOS(this);
			serial = new Serial(this, 0x3f8, () => pic.set_irq(4), uh.serial_write);
			kbd = new KBD(this, reset);
			reset_request = 0;
			if (uh.clipboard_get != null && uh.clipboard_set != null)
			{
				jsclipboard = new clipboard_device(this, 0x3c0, uh.clipboard_get, uh.clipboard_set, uh.get_boot_time);
			}
			cpu.ld8_port = ld8_port;
			cpu.ld16_port = ld16_port;
			cpu.ld32_port = ld32_port;
			cpu.st8_port = st8_port;
			cpu.st16_port = st16_port;
			cpu.st32_port = st32_port;
			cpu.get_hard_intno = () => pic.get_hard_intno();
		}

		private void st32_port()
		{
			throw new NotImplementedException();
		}

		private void st16_port()
		{
			throw new NotImplementedException();
		}

		private void st8_port()
		{
			throw new NotImplementedException();
		}

		private void ld32_port()
		{
			throw new NotImplementedException();
		}

		private void ld16_port()
		{
			throw new NotImplementedException();
		}

		private void ld8_port()
		{
			throw new NotImplementedException();
		}

		private object reset()
		{
			throw new NotImplementedException();
		}

		private void register_ioport_write(int i, int i1, int i2, object ioport80Write)
		{
			throw new NotImplementedException();
		}

		private void init_ioports()
		{
			throw new NotImplementedException();
		}
	}
}