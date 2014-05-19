using System;

namespace PCEmulator.Net.Utils
{
	public class Int32Array : BufferedArray<int>
	{
		public Int32Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}

		public int this[long mem8Loc]
		{
			get { throw new NotImplementedException(); }
			set
			{
				buffer[mem8Loc] = (byte) (value & 0xff);
				buffer[mem8Loc+1] = (byte)(value >> 8 & 0xff);
				buffer[mem8Loc+2] = (byte)(value >> 16 & 0xff);
				buffer[mem8Loc+3] = (byte)(value >> 24 & 0xff);
			}
		}
	}
}