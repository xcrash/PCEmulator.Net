namespace PCEmulator.Net.Utils
{
	public class Uint8Array : BufferedArray<byte>
	{
		public Uint8Array(byte[] buffer, int offset, uint size) : base(buffer, offset, size)
		{
		}
	}
}