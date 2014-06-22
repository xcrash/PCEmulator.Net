namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			public interface IOperand<T>
			{
				T readX();
				T setX { set; }

				T PopValue();
				void PushValue(T x);
			}

			public abstract class SingleOperandOp<T> : Op
			{
				protected IOperand<T> ctx;

				protected SingleOperandOp(Executor e, IOperand<T> ctx)
					: base(e)
				{
					this.ctx = ctx;
				}
			}

			public abstract class IncDecOp : SingleOperandOp<uint>
			{
				public enum Op
				{
					Inc = 27,
					Dec = 30
				}

				protected IncDecOp(Executor e, IOperand<uint> ctx)
					: base(e, ctx)
				{
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

				protected abstract void ExecInternal();
			}

			public class IncOp : IncDecOp
			{
				public IncOp(SingleOpContext<uint> ctx) : base(ctx.e, ctx)
				{
				}

				protected override void ExecInternal()
				{
					ctx.setX = e.u_dst = (ctx.readX() + 1) >> 0;
					e._op = (int) Op.Inc;
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
					ctx.setX = e.u_dst = (ctx.readX() - 1) >> 0;
					e._op = (int)Op.Dec;
				}
			}
		}
	}
}