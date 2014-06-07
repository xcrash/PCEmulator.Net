using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// CMOS Ram Memory, actually just the RTC Clock Emulator
	/// 
	/// Useful references:
	/// ------------------
	/// http://www.bioscentral.com/misc/cmosmap.htm
	/// http://wiki.osdev.org/CMOS
	/// </summary>
	public class CMOS
	{
		private readonly byte[] cmosData;
		private int cmosIndex;

		public CMOS(PCEmulator pc, DateTime? fixedDate = null)
		{
			var timeArray = new byte[128];
			cmosData = timeArray;
			cmosIndex = 0;
			var currentDate = fixedDate ?? DateTime.UtcNow;
			if (currentDate.Kind != DateTimeKind.Utc)
			{
				currentDate = currentDate.ToUniversalTime();
			}
			timeArray[0] = bin_to_bcd(currentDate.Second);
			timeArray[2] = bin_to_bcd(currentDate.Minute);
			timeArray[4] = bin_to_bcd(currentDate.Hour);
			timeArray[6] = bin_to_bcd((int) currentDate.DayOfWeek);
			timeArray[7] = bin_to_bcd(currentDate.Day);
			timeArray[8] = bin_to_bcd(currentDate.Month);
			timeArray[9] = bin_to_bcd(currentDate.Year % 100);
			timeArray[10] = 0x26;
			timeArray[11] = 0x02;
			timeArray[12] = 0x00;
			timeArray[13] = 0x80;
			timeArray[0x14] = 0x02;
			pc.register_ioport_write(0x70, 2, 1, ioport_write);
			pc.register_ioport_read(0x70, 2, 1, ioport_read);
		}


		/// <summary>
		/// In this implementation, bytes are stored in the RTC in BCD format
		/// binary -> bcd: bcd = ((bin / 10) &lt;&lt; 4) | (bin % 10)
		/// bcd -> binary: bin = ((bcd / 16) * 10) + (bcd & 0xf)
		/// </summary>
		/// <param name="a"></param>
		/// <returns></returns>
		private static byte bin_to_bcd(int a)
		{
			return (byte) (((a/10) << 4) | (a%10));
		}

		private byte ioport_read(uint mem8Loc)
		{
			if (mem8Loc == 0x70)
			{
				return 0xff;
			}

			// else here => 0x71, i.e., CMOS read
			var data = cmosData[cmosIndex];
			switch (cmosIndex)
			{
				case 10: // flip the UIP (update in progress) bit on a read
					cmosData[10] ^= 0x80;
					break;
				case 12: // Always return interrupt status == 0
					cmosData[12] = 0x00;
					break;
			}
			return data;
		}

		private void ioport_write(uint mem8Loc, byte data)
		{
			if (mem8Loc == 0x70)
			{
				// the high order bit is used to indicate NMI masking
				// low order bits are used to address CMOS
				// the index written here is used on an ioread 0x71
				cmosIndex = data & 0x7f;
			}
		}
	}
}