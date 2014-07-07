using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AdcOp : ArithmeticOpsEbGb
	{
		public AdcOp(EbOperand eb, GbOperand gb)
			: base(eb, gb)
		{
		}

		protected override uint CalculateResult(uint o0, uint o1)
		{
			var yb = o0;
			var ac = e.check_carry();
			e.u_src = o1;
			yb = (((yb + o1 + ac) << 24) >> 24);
			e.u_dst = yb;
			e._op = ac != 0 ? 3 : 0;
			return yb;
		}
	}
}