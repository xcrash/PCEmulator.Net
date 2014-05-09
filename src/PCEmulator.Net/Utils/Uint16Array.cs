namespace PCEmulator.Net.Utils
{
	public class Uint16Array : BufferedArray<ushort>
	{
		public Uint16Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}
	}
}