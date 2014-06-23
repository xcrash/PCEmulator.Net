namespace PCEmulator.Net
{
	public class IncOp : IncDecOp
	{
		public IncOp(CPU_X86_Impl.Executor.Operand<uint> ctx) : base(ctx.e, ctx)
		{
		}

		protected override void ExecInternal()
		{
			ctx.setX = e.u_dst = (ctx.readX() + 1) >> 0;
			e._op = (int) MagicEnum.Inc;
		}
	}
}