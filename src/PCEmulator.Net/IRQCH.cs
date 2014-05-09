using System;

namespace PCEmulator.Net
{
	public class IRQCH
	{
		private int count;
		private int latched_count;
		private int rw_state;
		public int mode;
		private int bcd;
		public object gate;
		private int count_load_time;
		private Func<uint> get_ticks;
		private double pit_time_unit;

		public IRQCH(Func<uint> cycle_count_callback)
		{
			this.count = 0;
			this.latched_count = 0;
			this.rw_state = 0;
			this.mode = 0;
			this.bcd = 0;
			this.gate = 0;
			this.count_load_time = 0;
			this.get_ticks = cycle_count_callback;
			this.pit_time_unit = (double)1193182 / 2000000;
		}

		private int get_time()
		{
			return (int) Math.Floor(this.get_ticks() * this.pit_time_unit);
		}

		public void pit_load_count(int x)
		{
			if (x == 0)
				x = 0x10000;
			this.count_load_time = this.get_time();
			this.count = x;
		}
	}
}