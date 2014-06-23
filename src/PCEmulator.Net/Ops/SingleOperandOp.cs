using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class SingleOperandOp<T> : Op
	{
		protected IOperand<T> ctx;

		protected SingleOperandOp(CPU_X86_Impl.Executor e, IOperand<T> ctx)
			: base(e)
		{
			this.ctx = ctx;
		}
	}
}