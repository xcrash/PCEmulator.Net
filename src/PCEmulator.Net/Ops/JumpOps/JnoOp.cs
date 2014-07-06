using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JnoOp : JumpOps
	{
		public JnoOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_overflow());
		}
	}
}