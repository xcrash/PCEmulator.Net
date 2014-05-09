using System;

namespace PCEmulator.Net
{
	public class IRQCH
	{
		public int LatchedCount;
		public int RwState;
		public int Mode;
		public int Bcd;
		public byte Gate;
		private int count;
		private int countLoadTime;
		private readonly Func<uint> getTicks;
		private readonly double pitTimeUnit;

		public IRQCH(Func<uint> cycleCountCallback)
		{
			count = 0;
			LatchedCount = 0;
			RwState = 0;
			Mode = 0;
			Bcd = 0;
			Gate = 0;
			countLoadTime = 0;
			getTicks = cycleCountCallback;
			pitTimeUnit = (double) 1193182/2000000;
		}

		public int pit_get_count()
		{
			int dh;
			var d = get_time() - countLoadTime;
			switch (Mode)
			{
				case 0:
				case 1:
				case 4:
				case 5:
					dh = (count - d) & 0xffff;
					break;
				default:
					dh = count - (d%count);
					break;
			}
			return dh;
		}

		public byte pit_get_out()
		{
			byte eh;
			var d = get_time() - countLoadTime;
			switch (Mode)
			{
				default:
// ReSharper disable once RedundantCaseLabel
				case 0: // Interrupt on terminal count
					eh = (byte) ((d >= count ? 1 : 0) >> 0);
					break;
				case 1: // One shot
					eh = (byte) ((d < count ? 1 : 0) >> 0);
					break;
				case 2: // Frequency divider
					if ((d%count) == 0 && d != 0)
						eh = 1;
					else
						eh = 0;
					break;
				case 3: // Square wave
					eh = (byte) (((d%count) < (count >> 1) ? 1 : 0) >> 0);
					break;
				case 4: // SW strobe
				case 5: // HW strobe
					eh = (byte) ((d == count ? 1 : 0) >> 0);
					break;
			}
			return eh;
		}

		public void pit_load_count(int x)
		{
			if (x == 0)
				x = 0x10000;
			countLoadTime = get_time();
			count = x;
		}

		private int get_time()
		{
			return (int) Math.Floor(getTicks()*pitTimeUnit);
		}
	}
}