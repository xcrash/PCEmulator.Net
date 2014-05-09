﻿using System;

namespace PCEmulator.Net
{
	public class PIC_Controller
	{
		private PIC[] pics;
		private int irq_requested;
		private Func<object> cpu_set_irq;
		private long last_irr;
		private int irr;

		public PIC_Controller(PCEmulator PC, int master_PIC_port, int slave_PIC_port, Func<object> cpu_set_irq_callback)
		{
			this.pics = new PIC[2];
			this.pics[0] = new PIC(PC, master_PIC_port);
			this.pics[1] = new PIC(PC, slave_PIC_port);
			this.pics[0].elcr_mask = 0xf8;
			this.pics[1].elcr_mask = 0xde;
			this.irq_requested = 0;
			this.cpu_set_irq = cpu_set_irq_callback;
			this.pics[0].update_irq = () => this.update_irq();
			this.pics[1].update_irq = () => this.update_irq();
		}

		public void set_irq(int irq, bool Qf)
		{
			var ir_register = 1 << irq;
			if (Qf)
			{
				if ((this.last_irr & ir_register) == 0)
					this.irr |= ir_register;
				this.last_irr |= ir_register;
			}
			else
			{
				this.last_irr &= ~ir_register;
			}
		}

		private object update_irq()
		{
			throw new NotImplementedException();
		}

		public void get_hard_intno()
		{
			throw new NotImplementedException();
		}
	}
}