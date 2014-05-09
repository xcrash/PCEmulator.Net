namespace PCEmulator.Net.Utils
{
	public class Uint8Array : BufferedArray<byte>
	{
		public Uint8Array(byte[] buffer, int offset, uint size) : base(buffer, offset, size)
		{
		}

		public byte this[uint mem8Loc]
		{
			get { throw new System.NotImplementedException(); }
			set { buffer[mem8Loc] = value; }
		}
	}
}