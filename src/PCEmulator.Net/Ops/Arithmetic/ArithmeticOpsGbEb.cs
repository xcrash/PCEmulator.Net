using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class ArithmeticOpsGbEb : ArithmeticOpSBase
	{
		private readonly GbOperand gb;

		public ArithmeticOpsGbEb(GbOperand gb, EbOperand eb)
			: base(gb, eb, gb.e)
		{
			this.gb = gb;
		}

		protected override uint CalculateResult(uint o0, uint o1)
		{
			return e.Calc(o0, o1);
		}

		protected override void ProceedResult(uint r)
		{
			gb.ProceedResult(r);
		}
	}
}