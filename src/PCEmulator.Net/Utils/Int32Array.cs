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
			get
			{
				return buffer[offset + mem8Loc*4]
					   | buffer[offset + mem8Loc * 4 + 1] << 8
					   | buffer[offset + mem8Loc * 4 + 2] << 16
					   | buffer[offset + mem8Loc * 4 + 3] << 24;
			}
			set
			{
				buffer[offset + mem8Loc * 4] = (byte)(value);
				buffer[offset + mem8Loc * 4 + 1] = (byte)(value >> 8);
				buffer[offset + mem8Loc*4 + 2] = (byte) (value >> 16);
				buffer[offset + mem8Loc*4 + 3] = (byte) (value >> 24);
			}
		}
	}
}