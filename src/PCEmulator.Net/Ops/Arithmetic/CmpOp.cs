using System;
using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class CmpOp : ArithmeticOpBase
	{
		private readonly EbOperand eb;
		private readonly GbOperand gb;

		public CmpOp(EbOperand eb, GbOperand gb) : base(eb, gb)
		{
			this.eb = eb;
			this.gb = gb;
		}

		protected override uint Calc(uint o0, uint o1)
		{
			var yb = o0;
			e.u_src = o1;
			e.u_dst = (uint) (((int) (yb - o1) << 24) >> 24);
			e._op = 6;
			return yb;
		}
	}
}