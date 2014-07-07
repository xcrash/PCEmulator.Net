using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class DoubleOperandOp<T> : Op
	{
		private readonly IOperand<T> o0;
		private readonly IOperand<T> o1;

		protected DoubleOperandOp(CPU_X86_Impl.Executor e, IOperand<T> o0, IOperand<T> o1)
			: base(e)
		{
			this.o0 = o0;
			this.o1 = o1;
		}

		public override void Exec()
		{
			var v0 = o0.ReadOpValue0();
			var v1 = o1.ReadOpValue1();

			var r = CalculateResult(v0, v1);

			o0.ProceedResult(r);
		}

		protected abstract uint CalculateResult(uint o0, uint o1);
	}
}