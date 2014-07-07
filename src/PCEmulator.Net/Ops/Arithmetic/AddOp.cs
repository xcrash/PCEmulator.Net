using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AddOp : ArithmeticOpsEbGb
	{
		public AddOp(EbOperand eb, GbOperand gb) : base(eb, gb)
		{
		}

		protected override uint CalculateResult(uint o0, uint o1)
		{
			var yb = o0;
			e.u_src = o1;
			yb = (((yb + o1) << 24) >> 24);
			e.u_dst = yb;
			e._op = 0;
			return yb;
		}
	}
}