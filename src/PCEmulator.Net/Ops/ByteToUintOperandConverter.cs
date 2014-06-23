using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class ByteToUintOperandConverter : IOperand<uint>
	{
		private readonly Operand<byte> inner;

		public ByteToUintOperandConverter(Operand<byte> inner)
		{
			this.inner = inner;
		}

		public uint readX()
		{
			return @uint(inner.readX());
		}

		public uint setX
		{
			set { inner.setX = (byte)value; }
		}

		public uint PopValue()
		{
			return inner.PopValue();
		}

		private static uint @uint(byte x)
		{
			return (uint)((x << 24) >> 24);
		}
	}
}