using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class ArithmeticOpsEbGb : ArithmeticOpSBase
	{
		private readonly EbOperand eb;

		protected ArithmeticOpsEbGb(EbOperand eb, GbOperand gb)
			: base(eb, gb, eb.e)
		{
			this.eb = eb;
		}

		protected override void ProceedResult(uint r)
		{
			eb.ProceedResult(r);
		}
	}
}