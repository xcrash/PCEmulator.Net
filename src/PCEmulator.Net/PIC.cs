using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// Common PC arrangements of IRQ lines:
	/// ------------------------------------
	/// 
	/// PC/AT and later systems had two 8259 controllers, master and
	/// slave. IRQ0 through IRQ7 are the master 8259's interrupt lines, while
	/// IRQ8 through IRQ15 are the slave 8259's interrupt lines. The labels on
	/// the pins on an 8259 are IR0 through IR7. IRQ0 through IRQ15 are the
	/// names of the ISA bus's lines to which the 8259s are attached.
	/// 
	/// Master 8259
	/// IRQ0 вЂ“ Intel 8253 or Intel 8254 Programmable Interval Timer, aka the system timer
	/// IRQ1 вЂ“ Intel 8042 keyboard controller
	/// IRQ2 вЂ“ not assigned in PC/XT; cascaded to slave 8259 INT line in PC/AT
	/// IRQ3 вЂ“ 8250 UART serial ports 2 and 4
	/// IRQ4 вЂ“ 8250 UART serial ports 1 and 3
	/// IRQ5 вЂ“ hard disk controller in PC/XT; Intel 8255 parallel ports 2 and 3 in PC/AT
	/// IRQ6 вЂ“ Intel 82072A floppy disk controller
	/// IRQ7 вЂ“ Intel 8255 parallel port 1 / spurious interrupt
	/// 
	/// Slave 8259 (PC/AT and later only)
	/// IRQ8 вЂ“ real-time clock (RTC)
	/// IRQ9 вЂ“ no common assignment, but 8-bit cards' IRQ2 line is routed to this interrupt.
	/// IRQ10 вЂ“ no common assignment
	/// IRQ11 вЂ“ no common assignment
	/// IRQ12 вЂ“ Intel 8042 PS/2 mouse controller
	/// IRQ13 вЂ“ math coprocessor
	/// IRQ14 вЂ“ hard disk controller 1
	/// IRQ15 вЂ“ hard disk controller 2
	/// </summary>
	public class PIC
	{
		public int ElcrMask;
		public Action UpdateIrq;
		private int lastIrr;
		private byte irr;
		private byte imr;
		private byte isr;
		private int priorityAdd;
		public int IrqBase;
		private bool readRegSelect;
		private int initState;
		private bool autoEoi;
		private bool init4;
		private int elcr;
		private bool rotateOnAutoEoi;

		public PIC(PCEmulator pc, int portNum)
		{
			pc.register_ioport_write(portNum, 2, 1, ioport_write);
			pc.register_ioport_read(portNum, 2, 1, ioport_read);
			Reset();
		}

		private void Reset()
		{
			lastIrr = 0;
			irr = 0; //Interrupt Request Register
			imr = 0; //Interrupt Mask Register
			isr = 0; //In-Service Register
			priorityAdd = 0;
			IrqBase = 0;
			readRegSelect = false;
			initState = 0;
			autoEoi = false;
			rotateOnAutoEoi = false;
			init4 = false;
			elcr = 0; // Edge/Level Control Register
			ElcrMask = 0;
		}

		public void set_irq1(int irq, bool qf)
		{
			var irRegister = (byte) (1 << irq);
			if (qf)
			{
				if ((lastIrr & irRegister) == 0)
					irr |= irRegister;
				lastIrr |= irRegister;
			}
			else
			{
				lastIrr &= ~irRegister;
			}
		}

		/// <summary>
		///   The priority assignments for IRQ0-7 seem to be maintained in a
		/// cyclic order modulo 8 by the 8259A.  On bootup, it default to:
		/// 
		/// Priority: 0 1 2 3 4 5 6 7
		/// IRQ:      7 6 5 4 3 2 1 0
		/// 
		/// but can be rotated automatically or programmatically to a state e.g.:
		/// 
		/// Priority: 5 6 7 0 1 2 3 4
		/// IRQ:      7 6 5 4 3 2 1 0
		/// </summary>
		/// <param name="irRegister"></param>
		/// <returns></returns>
		private int get_priority(byte irRegister)
		{
			if (irRegister == 0)
				return -1;
			var priority = 7;
			while ((irRegister & (1 << ((priority + priorityAdd) & 7))) == 0)
				priority--;
			return priority;
		}

		public byte get_irq()
		{
			var irRegister = (byte) (irr & ~imr);
			var priority = get_priority(irRegister);
			if (priority < 0)
				return Convert.ToByte(-1);
			var inServicePriority = get_priority(isr);
			if (priority > inServicePriority)
			{
				return (byte) priority;
			}

			return Convert.ToByte(-1);
		}

		public void Intack(byte irq)
		{
			if (autoEoi)
			{
				if (rotateOnAutoEoi)
					priorityAdd = (irq + 1) & 7;
			}
			else
			{
				isr |= (byte) (1 << irq);
			}
			if ((elcr & (1 << irq)) == 0)
				irr &= (byte) ~(1 << irq);
		}

		private void ioport_write(uint mem8Loc, byte x)
		{
			mem8Loc &= 1;
			if (mem8Loc == 0)
			{
				if ((x & 0x10) != 0)
				{
					/*
					  ICW1
					  // 7:5 = address (if MCS-80/85 mode)
					  // 4 == 1
					  // 3: 1 == level triggered, 0 == edge triggered
					  // 2: 1 == call interval 4, 0 == call interval 8
					  // 1: 1 == single PIC, 0 == cascaded PICs
					  // 0: 1 == send ICW4

					 */
					Reset();
					initState = 1;
					init4 = (x & 1) != 0;
					if ((x & 0x02) != 0)
						throw new Exception("single mode not supported");
					if ((x & 0x08) != 0)
						throw new Exception("level sensitive irq not supported");
				}
				else if ((x & 0x08) != 0)
				{
					if ((x & 0x02) != 0)
						readRegSelect = (x & 1) != 0;
				}
				else
				{
					switch (x)
					{
						case 0x00:
						case 0x80:
							rotateOnAutoEoi = (x >> 7) != 0;
							break;
						case 0x20:
						case 0xa0:
							var priority = get_priority(isr);
							if (priority >= 0)
							{
								isr &= Convert.ToByte(~(1 << ((priority + priorityAdd) & 7)));
							}
							if (x == 0xa0)
								priorityAdd = (priorityAdd + 1) & 7;
							break;
						case 0x60:
						case 0x61:
						case 0x62:
						case 0x63:
						case 0x64:
						case 0x65:
						case 0x66:
						case 0x67:
							priority = x & 7;
							isr &= Convert.ToByte(~(1 << priority));
							break;
						case 0xc0:
						case 0xc1:
						case 0xc2:
						case 0xc3:
						case 0xc4:
						case 0xc5:
						case 0xc6:
						case 0xc7:
							priorityAdd = (x + 1) & 7;
							break;
						case 0xe0:
						case 0xe1:
						case 0xe2:
						case 0xe3:
						case 0xe4:
						case 0xe5:
						case 0xe6:
						case 0xe7:
							priority = x & 7;
							isr &= Convert.ToByte(~(1 << priority));
							priorityAdd = (priority + 1) & 7;
							break;
					}
				}
			}
			else
			{
				switch (initState)
				{
					case 0:
						imr = x;
						UpdateIrq();
						break;
					case 1:
						IrqBase = x & 0xf8;
						initState = 2;
						break;
					case 2:
						initState = init4 ? 3 : 0;
						break;
					case 3:
						autoEoi = ((x >> 1) & 1) != 0;
						initState = 0;
						break;
				}
			}
		}

		private byte ioport_read(uint mem8Loc)
		{
			byte returnRegister;
			mem8Loc = mem8Loc & 1;
			if (mem8Loc == 0)
			{
				returnRegister = readRegSelect ? isr : irr;
			}
			else
			{
				returnRegister = imr;
			}
			return returnRegister;
		}
	}
}