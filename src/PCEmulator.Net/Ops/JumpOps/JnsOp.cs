using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JnsOp : JumpOps
	{
		public JnsOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_sign());
		}
	}
}