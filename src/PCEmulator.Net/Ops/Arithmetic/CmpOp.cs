using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class CmpOp : ArithmeticOpsEbGb
	{
		public CmpOp(EbOperand eb, GbOperand gb) : base(eb, gb)
		{
		}

		protected override uint CalculateResult(uint o0, uint o1)
		{
			var yb = o0;
			e.u_src = o1;
			e.u_dst = (uint) (((int) (yb - o1) << 24) >> 24);
			e._op = 6;
			return yb;
		}
	}
}