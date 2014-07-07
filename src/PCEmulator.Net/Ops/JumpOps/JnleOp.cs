using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnleOp : JumpOps
	{
		public JnleOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_less_or_equal());
		}
	}
}