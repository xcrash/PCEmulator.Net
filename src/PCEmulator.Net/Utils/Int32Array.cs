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
				return buffer[offset + mem8Loc] << 24
				       | buffer[offset + mem8Loc + 1] << 16
				       | buffer[offset + mem8Loc + 2] << 8
				       | buffer[offset + mem8Loc + 3];
			}
			set
			{
				buffer[offset + mem8Loc] = (byte)(value >> 24);
				buffer[offset + mem8Loc + 1] = (byte)(value >> 16);
				buffer[offset + mem8Loc + 2] = (byte)(value >> 8);
				buffer[offset + mem8Loc + 3] = (byte)(value);
			}
		}
	}
}