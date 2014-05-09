using System;

namespace PCEmulator.Net
{
	public class PIC
	{
		public int elcr_mask;
		public Action update_irq;
		private int last_irr;
		private int irr;
		private int imr;
		private int isr;
		private int priority_add;
		private int irq_base;
		private int read_reg_select;
		private int special_mask;
		private int init_state;
		private int auto_eoi;
		private int rotate_on_autoeoi;
		private int init4;
		private int elcr;

		public PIC(PCEmulator PC, int port_num)
		{
			PC.register_ioport_write(port_num, 2, 1, ioport_write);
			PC.register_ioport_read(port_num, 2, 1, ioport_read);
			this.reset();
		}

		private void reset()
		{
			this.last_irr = 0;
			this.irr = 0; //Interrupt Request Register
			this.imr = 0; //Interrupt Mask Register
			this.isr = 0; //In-Service Register
			this.priority_add = 0;
			this.irq_base = 0;
			this.read_reg_select = 0;
			this.special_mask = 0;
			this.init_state = 0;
			this.auto_eoi = 0;
			this.rotate_on_autoeoi = 0;
			this.init4 = 0;
			this.elcr = 0; // Edge/Level Control Register
			this.elcr_mask = 0;
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