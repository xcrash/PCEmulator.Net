namespace PCEmulator.Net.Utils
{
	public class Uint16Array : BufferedArray<ushort>
	{
		public Uint16Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}

		public override ushort this[uint mem8Loc]
		{
			get
			{
				return (ushort)(buffer[offset + mem8Loc * 2]
							   | buffer[offset + mem8Loc * 2 + 1] << 8);
			}
			set
			{
				buffer[offset + mem8Loc * 2] = (byte)(value);
				buffer[offset + mem8Loc * 2 + 1] = (byte)(value >> 8);
			}
		}

		public override ushort this[long mem8Loc]
		{
			get { return this[(uint)mem8Loc]; }
			set { this[(uint)mem8Loc] = value; }
		}
	}
}