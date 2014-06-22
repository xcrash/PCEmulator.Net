namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			public class PopOp : SingleOperandOp<uint>
			{
				public PopOp(SingleOpContext<uint> ctx)
					: base(ctx.e, ctx)
				{
				}

				public override void Exec()
				{
					ctx.setX = ctx.PopValue();
				}
			}

			public class PushOp<T> : SingleOperandOp<T>
			{
				public PushOp(SingleOpContext<T> ctx)
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
	}
}