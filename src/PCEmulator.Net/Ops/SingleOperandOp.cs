namespace PCEmulator.Net
{
	public abstract class SingleOperandOp<T> : Op
	{
		protected CPU_X86_Impl.Executor.IOperand<T> ctx;

		protected SingleOperandOp(CPU_X86_Impl.Executor e, CPU_X86_Impl.Executor.IOperand<T> ctx)
			: base(e)
		{
			this.ctx = ctx;
		}
	}
}