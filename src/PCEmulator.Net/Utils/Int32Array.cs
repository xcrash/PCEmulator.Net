using System;

namespace PCEmulator.Net.Utils
{
	public class Int32Array : BufferedArray<int>
	{
		public Int32Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}

		public int this[uint mem8Loc]
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}
	}
}