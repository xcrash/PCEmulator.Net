namespace PCEmulator.Net.Utils
{
	public class Uint16Array : BufferedArray<ushort>
	{
		public Uint16Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}

		public uint this[long mem8Loc]
		{
			get
			{
				return (uint) (buffer[offset + mem8Loc * 2] << 8
				               | buffer[offset + mem8Loc * 2 + 1]);
			}
			set
			{
				buffer[offset + mem8Loc * 2] = (byte)(value >> 8);
				buffer[offset + mem8Loc * 2 + 1] = (byte)(value);
			}
		}
	}
}