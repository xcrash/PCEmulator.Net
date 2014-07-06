using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class OrOp : ArithmeticOpBase
	{
		public OrOp(EbOperand eb, GbOperand gb) : base(eb, gb)
		{
		}

		protected override uint Calculate(uint Yb, uint Zb)
		{
			Yb = (uint) ((int) ((Yb | Zb) << 24) >> 24);
			e.u_dst = Yb;
			e._op = 12;
			return Yb;
		}
	}
}