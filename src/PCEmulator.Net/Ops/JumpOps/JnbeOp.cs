using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnbeOp : JumpOps
	{
		public JnbeOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_below_or_equal());
		}
	}
}