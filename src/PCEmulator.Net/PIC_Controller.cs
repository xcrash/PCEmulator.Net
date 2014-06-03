using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// 8259A PIC (Programmable Interrupt Controller) Emulation Code
	/// 
	/// The 8259 combines multiple interrupt input sources into a single
	/// interrupt output to the host microprocessor, extending the interrupt
	/// levels available in a system beyond the one or two levels found on the
	/// processor chip.
	/// 
	/// There are three registers, an Interrupt Mask Register (IMR), an
	/// Interrupt Request Register (IRR), and an In-Service Register
	/// (ISR):
	/// IRR - a mask of the current interrupts that are pending acknowledgement
	/// ISR - a mask of the interrupts that are pending an EOI
	/// IMR - a mask of interrupts that should not be sent an acknowledgement
	/// 
	/// End Of Interrupt (EOI) operations support specific EOI, non-specific
	/// EOI, and auto-EOI. A specific EOI specifies the IRQ level it is
	/// acknowledging in the ISR. A non-specific EOI resets the IRQ level in
	/// the ISR. Auto-EOI resets the IRQ level in the ISR immediately after
	/// the interrupt is acknowledged.
	/// 
	/// After the IBM XT, it was decided that 8 IRQs was not enough.
	/// The backwards-compatible solution was simply to chain two 8259As together,
	/// the master and slave PIC.
	/// 
	/// Useful References
	/// -----------------
	/// https://en.wikipedia.org/wiki/Programmable_Interrupt_Controller
	/// https://en.wikipedia.org/wiki/Intel_8259
	/// http://www.thesatya.com/8259.html
	/// </summary>
	public class PIC_Controller
	{
		private readonly PIC[] pics;
		private readonly Action<int> cpuSetIrq;
		private long lastIrr;
		private int irr;

		public PIC_Controller(PCEmulator PC, int master_PIC_port, int slave_PIC_port, Action<int> cpu_set_irq_callback)
		{
			this.pics = new PIC[2];
			this.pics[0] = new PIC(PC, master_PIC_port);
			this.pics[1] = new PIC(PC, slave_PIC_port);
			this.pics[0].ElcrMask = 0xf8;
			this.pics[1].ElcrMask = 0xde;
			this.cpuSetIrq = cpu_set_irq_callback;
			this.pics[0].UpdateIrq = () => this.update_irq();
			this.pics[1].UpdateIrq = () => this.update_irq();
		}

		public void set_irq(int irq, bool Qf)
		{
			this.pics[irq >> 3].set_irq1(irq & 7, Qf);
			this.update_irq();
		}

		public int get_hard_intno()
		{
			int intno;
			var irq = this.pics[0].get_irq();
			if (irq >= 0)
			{
				this.pics[0].Intack(irq);
				if (irq == 2)
				{ //IRQ 2 cascaded to slave 8259 INT line in PC/AT
					var slave_irq = this.pics[1].get_irq();
					if (slave_irq >= 0)
					{
						this.pics[1].Intack(slave_irq);
					}
					else
					{
						slave_irq = 7;
					}
					intno = this.pics[1].IrqBase + slave_irq;
					irq = (byte) (slave_irq + 8);
				}
				else
				{
					intno = this.pics[0].IrqBase + irq;
				}
			}
			else
			{
				irq = 7;
				intno = this.pics[0].IrqBase + irq;
			}
			this.update_irq();
			return intno;
		}

		private void update_irq()
		{
			var slave_irq = this.pics[1].get_irq();
			if (slave_irq >= 0)
			{
				this.pics[0].set_irq1(2, true);
				this.pics[0].set_irq1(2, false);
			}
			var irq = this.pics[0].get_irq();
			if (irq >= 0)
			{
				this.cpuSetIrq(1);
			}
			else
			{
				this.cpuSetIrq(0);
			}
		}
	}
}