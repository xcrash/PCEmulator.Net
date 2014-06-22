namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			public class PopOp : Op
			{
				private readonly SingleOpContext<uint> ctx;

				public PopOp(SingleOpContext<uint> ctx)
					: base(ctx.e)
				{
					this.ctx = ctx;
				}

				public override void Exec()
				{
					ctx.setX = ctx.PopValue();
				}
			}

			public class PushOp<T> : Op
			{
				private readonly SingleOpContext<T> ctx;

				public PushOp(SingleOpContext<T> ctx)
					: base(ctx.e)
				{
					this.ctx = ctx;
				}

				public override void Exec()
				{
					var x = ctx.readXuint();
					ctx.PushValue(x);
				}
			}
		}
	}
}