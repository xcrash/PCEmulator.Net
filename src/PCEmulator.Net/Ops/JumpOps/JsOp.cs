using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JsOp : JumpOps
	{
		public JsOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_sign());
		}
	}
}