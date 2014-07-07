using PCEmulator.Net.Operands;

namespace PCEmulator.Net.PushPopOps
{
	public class PopOp : SingleOperandOp<uint>
	{
		public PopOp(Operand<uint> o)
			: base(o.e, o)
		{
		}

		public override void Exec()
		{
			o.setX = o.PopValue();
		}
	}
}