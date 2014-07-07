using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	public abstract class Operand<T> : OpContext, IOperand<T>
	{
		public IArgumentOperand<T> ops { get; set; }

		protected Operand(IArgumentOperand<T> ops, CPU_X86_Impl.Executor e)
			: base(e)
		{
			this.ops = ops;
		}

		public virtual T readX()
		{
			return ops.readX();
		}

		public virtual T setX
		{
			set { ops.setX = value; }
		}

		public abstract T PopValue();

		public abstract uint ReadOpValue0();
		public abstract uint ReadOpValue1();
		public abstract void ProceedResult(uint r);
	}
}