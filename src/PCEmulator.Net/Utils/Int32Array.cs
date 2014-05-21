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
			get { return buffer[mem8Loc] + buffer[mem8Loc + 1] >> 8 + buffer[mem8Loc + 2] >> 16 + buffer[mem8Loc + 3] >> 24; }
			set
			{
				buffer[mem8Loc] = (byte) (value);
				buffer[mem8Loc + 1] = (byte) (value >> 8);
				buffer[mem8Loc + 2] = (byte) (value >> 16);
				buffer[mem8Loc + 3] = (byte) (value >> 24);
			}
		}
	}
}