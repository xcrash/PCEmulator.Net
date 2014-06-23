using PCEmulator.Net.Operands;

namespace PCEmulator.Net
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