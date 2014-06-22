namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			public abstract class IncDecOp : Op
			{
				protected readonly SingleOpContext<uint> ctx;

				protected IncDecOp(Executor e, SingleOpContext<uint> ctx)
					: base(e)
				{
					this.ctx = ctx;
				}

				public override void Exec()
				{
					Init();
					ExecInternal();
				}

				protected void Init()
				{
					e.reg_idx1 = (int) (e.OPbyteRegIdx0);
					if (e._op < 25)
					{
						e._op2 = e._op;
						e._dst2 = (int) e.u_dst;
					}
				}

				protected void Inc()
				{
					ctx.setX = e.u_dst = (ctx.readX() + 1) >> 0;
					e._op = 27;
				}

				protected void Dec()
				{
					ctx.setX = e.u_dst = (ctx.readX() - 1) >> 0;
					e._op = 30;
				}

				protected abstract void ExecInternal();
			}

			public class IncOp : IncDecOp
			{
				public IncOp(SingleOpContext<uint> ctx) : base(ctx.e, ctx)
				{
				}

				protected override void ExecInternal()
				{
					Inc();
				}
			}

			public class DecOp : IncDecOp
			{
				public DecOp(SingleOpContext<uint> ctx)
					: base(ctx.e, ctx)
				{
				}

				protected override void ExecInternal()
				{
					Dec();
				}
			}
		}
	}
}