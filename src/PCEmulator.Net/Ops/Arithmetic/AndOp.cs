using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AndOp : ArithmeticOpBase
	{
		public AndOp(EbOperand eb, GbOperand gb)
			: base(eb, gb)
		{
		}

		protected override uint Calc(uint o0, uint o1)
		{
			var yb = o0;
			yb = (((yb & o1) << 24) >> 24);
			e.u_dst = yb;
			e._op = 12;
			return yb;
		}
	}
}