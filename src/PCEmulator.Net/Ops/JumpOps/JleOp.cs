using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JleOp : JumpOps
	{
		public JleOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_less_or_equal());
		}
	}
}