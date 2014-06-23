namespace PCEmulator.Net
{
	public class PushOp<T> : SingleOperandOp<T>
	{
		public PushOp(CPU_X86_Impl.Executor.Operand<T> ctx)
			: base(ctx.e, ctx)
		{
		}

		public override void Exec()
		{
			var x = ctx.readX();
			ctx.PushValue(x);
		}
	}
}