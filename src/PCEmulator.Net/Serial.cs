using System;
using System.Text;

namespace PCEmulator.Net
{
	/// <summary>
	/// Serial Controller Emulator
	/// </summary>
	public class Serial
	{
		private int divider;
		private byte rbr;
		private byte ier;
		private byte iir;
		private byte lcr;
		private byte lsr;
		private byte msr;
		private byte scr;
		private readonly Action<bool> setIrqFunc;
		private readonly Action<char> writeFunc;
		private string receiveFifo;
		private byte mcr;

		public Serial(PCEmulator ng, int port, Action<bool> kh, Action<char> lh)
		{
			divider = 0;
			rbr = 0;
			ier = 0;
			iir = 0x01;
			lcr = 0;
			//this.mcr;
			lsr = 0x40 | 0x20;
			msr = 0;
			scr = 0;
			setIrqFunc = kh;
			writeFunc = lh;
			receiveFifo = "";
			ng.register_ioport_write(port, 8, 1, ioport_write);
			ng.register_ioport_read(port, 8, 1, (Func<uint, byte>) ioport_read);
		}

		private void update_irq()
		{
			if ((lsr & 0x01) != 0 && (ier & 0x01) != 0)
			{
				iir = 0x04;
			}
			else if ((lsr & 0x20) != 0 && (ier & 0x02) != 0)
			{
				iir = 0x02;
			}
			else
			{
				iir = 0x01;
			}
// ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
			if (iir != 0x01)
			{
				setIrqFunc(true);
			}
			else
			{
				setIrqFunc(false);
			}
		}

		private void ioport_write(uint mem8Loc, byte x)
		{
			mem8Loc &= 7;
			switch (mem8Loc)
			{
				default:
// ReSharper disable once RedundantCaseLabel
				case 0:
					if ((lcr & 0x80) != 0)
					{
						divider = (divider & 0xff00) | x;
					}
					else
					{
						lsr = (byte) (lsr & ~0x20);
						update_irq();
						writeFunc(Encoding.ASCII.GetString(new[] {x})[0]);
						lsr |= 0x20;
						lsr |= 0x40;
						update_irq();
					}
					break;
				case 1:
					if ((lcr & 0x80) != 0)
					{
						divider = (divider & 0x00ff) | (x << 8);
					}
					else
					{
						ier = x;
						update_irq();
					}
					break;
				case 2:
					break;
				case 3:
					lcr = x;
					break;
				case 4:
					mcr = x;
					break;
				case 5:
					break;
				case 6:
					msr = x;
					break;
				case 7:
					scr = x;
					break;
			}
		}

		private byte ioport_read(uint mem8Loc)
		{
			byte res;
			mem8Loc &= 7;
			switch (mem8Loc)
			{
				default:
// ReSharper disable once RedundantCaseLabel
				case 0:
					if ((lcr & 0x80) != 0)
					{
						res = (byte) (divider & 0xff);
					}
					else
					{
						res = rbr;
						lsr = (byte) (lsr & ~(0x01 | 0x10));
						update_irq();
						send_char_from_fifo();
					}
					break;
				case 1:
					if ((lcr & 0x80) != 0)
					{
						res = (byte) ((divider >> 8) & 0xff);
					}
					else
					{
						res = ier;
					}
					break;
				case 2:
					res = iir;
					break;
				case 3:
					res = lcr;
					break;
				case 4:
					res = mcr;
					break;
				case 5:
					res = lsr;
					break;
				case 6:
					res = msr;
					break;
				case 7:
					res = scr;
					break;
			}
			return res;
		}

		private void send_char(char mh)
		{
			rbr = Convert.ToByte(mh);
			lsr |= 0x01;
			update_irq();
		}

		private void send_char_from_fifo()
		{
			var nh = receiveFifo;
			if (nh != "" && (lsr & 0x01) == 0)
			{
				send_char(nh[0]);
				receiveFifo = nh.Substring(1, nh.Length - 1);
			}
		}

		public void send_chars(string na)
		{
			receiveFifo += na;
			send_char_from_fifo();
		}
	}
}