using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class PopOp : SingleOperandOp<uint>
	{
		public PopOp(Operand<uint> ctx)
			: base(ctx.e, ctx)
		{
		}

		public override void Exec()
		{
			ctx.setX = ctx.PopValue();
		}
	}
}