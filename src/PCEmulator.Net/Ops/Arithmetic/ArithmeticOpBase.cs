using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class ArithmeticOpBase : Op
	{
		protected readonly EbOperand eb;
		protected readonly GbOperand gb;

		protected ArithmeticOpBase(EbOperand eb, GbOperand gb)
			: base(eb.e)
		{
			this.eb = eb;
			this.gb = gb;
		}

		public override void Exec()
		{
			var o0 = eb.ReadOpValue0();
			var o1 = gb.ReadOpValue1();

			var r = Calc(o0, o1);

			WriteResult(r);
		}

		private void WriteResult(uint r)
		{
			if (e.isRegisterAddressingMode)
			{
				e.set_word_in_register(e.reg_idx0, r);
			}
			else
			{
				x = r;
				e.st8_mem8_write(x);
			}
		}

		protected abstract uint Calc(uint o0, uint o1);
	}
}