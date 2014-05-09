namespace PCEmulator.Net.Utils
{
	public class Int32Array : BufferedArray<int>
	{
		public Int32Array(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}
	}
}