using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// support only getBootTime
	/// </summary>
	public class Clipboard
	{
		private readonly Func<int> getBootTime;

		public Clipboard(PCEmulator pc, int @base, Func<int> getBootTimeFunc)
		{
			pc.register_ioport_read(@base, 16, 4, (Func<uint, uint>) ioport_readl);
			getBootTime = getBootTimeFunc;
		}

		private uint ioport_readl(uint mem8_loc)
		{
			mem8_loc = (mem8_loc >> 2) & 3;
			switch (mem8_loc)
			{
				case 3:
					if (getBootTime != null)
						return (uint) getBootTime();
					break;
			}
			return 0;
		}
	}
}