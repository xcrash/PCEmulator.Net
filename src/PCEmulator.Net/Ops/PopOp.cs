namespace PCEmulator.Net
{
	public class PopOp : SingleOperandOp<uint>
	{
		public PopOp(CPU_X86_Impl.Executor.Operand<uint> ctx)
			: base(ctx.e, ctx)
		{
		}

		public override void Exec()
		{
			ctx.setX = ctx.PopValue();
		}
	}
}