using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// 8254 Programmble Interrupt Timer Emulator
	/// 
	/// Useful References
	/// -----------------
	/// https://en.wikipedia.org/wiki/Intel_8253
	/// </summary>
	public class PIT
	{
		private readonly IRQCH[] pitChannels;
		private byte speakerDataOn;
		private readonly Action<bool> setIrq;

		public PIT(PCEmulator pc, Action<bool> setIrqCallback, Func<uint> cycleCountCallback)
		{
			pitChannels = new IRQCH[3];
			for (var i = 0; i < 3; i++)
			{
				var s = new IRQCH(cycleCountCallback);
				pitChannels[i] = s;
				s.Mode = 3;
				s.Gate = (byte) ((i != 2) ? 1 : 0);
				s.pit_load_count(0);
			}
			speakerDataOn = 0;
			setIrq = setIrqCallback;
			// Ports:
			// 0x40: Channel 0 data port
			// 0x61: Control
			pc.register_ioport_write(0x40, 4, 1, ioport_write);
			pc.register_ioport_read(0x40, 3, 1, (Func<uint, byte>) ioport_read);
			pc.register_ioport_read(0x61, 1, 1, (Func<uint, byte>) speaker_ioport_read);
			pc.register_ioport_write(0x61, 1, 1, speaker_ioport_write);
		}

		public void update_irq()
		{
			setIrq(true);
			setIrq(false);
		}

		private void ioport_write(uint mem8Loc, byte x)
		{
			mem8Loc &= 3;
			if (mem8Loc == 3)
			{
				var hh = x >> 6;
				if (hh == 3)
					return;
				var s = pitChannels[hh];
				var ih = (x >> 4) & 3;
				switch (ih)
				{
					case 0:
						s.LatchedCount = s.pit_get_count();
						s.RwState = 4;
						break;
					default:
						s.Mode = (x >> 1) & 7;
						s.Bcd = x & 1;
						s.RwState = ih - 1 + 0;
						break;
				}
			}
			else
			{
				var s = pitChannels[mem8Loc];
				switch (s.RwState)
				{
					case 0:
						s.pit_load_count(x);
						break;
					case 1:
						s.pit_load_count(x << 8);
						break;
					case 2:
					case 3:
						if ((s.RwState & 1) != 0)
						{
							s.pit_load_count((s.LatchedCount & 0xff) | (x << 8));
						}
						else
						{
							s.LatchedCount = x;
						}
						s.RwState ^= 1;
						break;
				}
			}
		}

		private byte ioport_read(uint mem8Loc)
		{
			byte res;
			mem8Loc &= 3;
			var s = pitChannels[mem8Loc];
			switch (s.RwState)
			{
				case 0:
				case 1:
				case 2:
				case 3:
					var ma = s.pit_get_count();
					if ((s.RwState & 1) != 0)
						res = (byte) ((ma >> 8) & 0xff);
					else
						res = (byte) (ma & 0xff);
					if ((s.RwState & 2) != 0)
						s.RwState ^= 1;
					break;
				default:
// ReSharper disable RedundantCaseLabel
				case 4:
				case 5:
// ReSharper restore RedundantCaseLabel
					if ((s.RwState & 1) != 0)
						res = (byte) (s.LatchedCount >> 8);
					else
						res = (byte) (s.LatchedCount & 0xff);
					s.RwState ^= 1;
					break;
			}
			return res;
		}

		private void speaker_ioport_write(uint mem8Loc, byte x)
		{
			speakerDataOn = (byte) ((x >> 1) & 1);
			pitChannels[2].Gate = (byte) (x & 1);
		}

		private byte speaker_ioport_read(uint mem8Loc)
		{
			var s = pitChannels[2];
			var eh = s.pit_get_out();
			var x = (speakerDataOn << 1) | s.Gate | (eh << 5);
			return (byte) x;
		}
	}
}