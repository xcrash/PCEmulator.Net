using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class ArithmeticOpBase : Op
	{
		private readonly EbOperand eb;
		private readonly GbOperand gb;

		protected ArithmeticOpBase(EbOperand eb, GbOperand gb)
			: base(eb.e)
		{
			this.eb = eb;
			this.gb = gb;
		}

		public override void Exec()
		{
			e.y = e.readY(eb);
			e.x = e.readX(gb);

			e.x = Calculate(e.x, e.y);

			e.setX(eb, gb, e.x);
		}

		protected abstract uint Calculate(uint x, uint a);
	}
}