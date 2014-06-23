using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class PushOp<T> : SingleOperandOp<T>
	{
		public PushOp(Operand<T> ctx)
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