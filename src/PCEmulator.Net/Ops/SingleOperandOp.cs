using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class SingleOperandOp<T> : Op
	{
		protected IOperand<T> o;

		protected SingleOperandOp(CPU_X86_Impl.Executor e, IOperand<T> o)
			: base(e)
		{
			this.o = o;
		}
	}
}