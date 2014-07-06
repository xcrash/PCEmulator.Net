using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AddOp : ArithmeticOpBase
	{
		public AddOp(EbOperand eb, GbOperand gb)
			: base(eb, gb)
		{
		}

		protected override uint Calculate(uint x, uint a)
		{
			e.u_src = a;
			x = (((x + a) << 24) >> 24);
			e.u_dst = x;
			e._op = 0;
			return x;
		}
	}
}