namespace PCEmulator.Net.Utils
{
	public class Uint8Array : BufferedArray<byte>
	{
		public Uint8Array(byte[] buffer, int offset, uint size) : base(buffer, offset, size)
		{
		}

		public override byte this[uint mem8Loc]
		{
			get { return buffer[mem8Loc]; }
			set { buffer[mem8Loc] = value; }
		}

		public override byte this[long mem8Loc]
		{
			get { return buffer[(uint) mem8Loc]; }
			set { buffer[(uint) mem8Loc] = value; }
		}
	}
}