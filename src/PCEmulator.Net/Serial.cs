using System;

namespace PCEmulator.Net
{
	public class Serial
	{
		private int divider;
		private int rbr;
		private int ier;
		private int iir;
		private int lcr;
		private int lsr;
		private int msr;
		private int scr;
		private object set_irq_func;
		private object write_func;
		private string receive_fifo;

		public Serial(PCEmulator Ng, int i, Func<object> kh, object lh)
		{
			this.divider = 0;
			this.rbr = 0;
			this.ier = 0;
			this.iir = 0x01;
			this.lcr = 0;
			//this.mcr;
			this.lsr = 0x40 | 0x20;
			this.msr = 0;
			this.scr = 0;
			this.set_irq_func = kh;
			this.write_func = lh;
			this.receive_fifo = "";
			Ng.register_ioport_write(0x3f8, 8, 1, ioport_write);
			Ng.register_ioport_read(0x3f8, 8, 1, ioport_read);
		}

		public void send_chars(char str)
		{
			throw new NotImplementedException();
		}

		private byte ioport_read(uint mem8Loc)
		{
			throw new NotImplementedException();
		}

		private void ioport_write(uint mem8Loc, byte data)
		{
			throw new NotImplementedException();
		}
	}
}