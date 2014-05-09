using System;

namespace PCEmulator.Net
{
	public class CMOS
	{
		private byte[] cmos_data;
		private int cmos_index;

		public CMOS(PCEmulator PC)
		{
			var time_array = new byte[128];
			this.cmos_data = time_array;
			this.cmos_index = 0;
			var d = new DateTime();
			time_array[0] = bin_to_bcd(DateTime.UtcNow.Second);
			time_array[2] = bin_to_bcd(DateTime.UtcNow.Minute);
			time_array[4] = bin_to_bcd(DateTime.UtcNow.Hour);
			time_array[6] = bin_to_bcd((int) DateTime.UtcNow.DayOfWeek);
			time_array[7] = bin_to_bcd(DateTime.UtcNow.Day);
			time_array[8] = bin_to_bcd(DateTime.UtcNow.Month);
			time_array[9] = bin_to_bcd(DateTime.UtcNow.Year % 100);
			time_array[10] = 0x26;
			time_array[11] = 0x02;
			time_array[12] = 0x00;
			time_array[13] = 0x80;
			time_array[0x14] = 0x02;
			PC.register_ioport_write(0x70, 2, 1, ioport_write);
			PC.register_ioport_read(0x70, 2, 1, ioport_read);
		}

		private byte bin_to_bcd(int a)
		{
			return (byte) (((a/10) << 4) | (a%10));
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