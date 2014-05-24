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
				return buffer[offset + mem8Loc*4] << 24
				       | buffer[offset + mem8Loc*4 + 1] << 16
				       | buffer[offset + mem8Loc*4 + 2] << 8
				       | buffer[offset + mem8Loc*4 + 3];
			}
			set
			{
				buffer[offset + mem8Loc*4] = (byte) (value >> 24);
				buffer[offset + mem8Loc*4 + 1] = (byte) (value >> 16);
				buffer[offset + mem8Loc*4 + 2] = (byte) (value >> 8);
				buffer[offset + mem8Loc*4 + 3] = (byte) (value);
			}
		}
	}
}