using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JoOp : JumpOps
	{
		public JoOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_overflow());
		}
	}
}