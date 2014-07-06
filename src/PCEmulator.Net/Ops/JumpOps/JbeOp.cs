using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JbeOp : JumpOps
	{
		public JbeOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_below_or_equal());
		}
	}
}