using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public abstract class JumpOps : SingleOperandOp<byte>
	{
		//TODO: use base class where possible
		protected new JbOperand o;

		protected JumpOps(JbOperand o)
			: base(o.e, o)
		{
			this.o = o;
		}

		protected void Jump(bool doJump)
		{
			if (doJump)
			{
				x = o.readX();
				physmem8_ptr = (physmem8_ptr + x) >> 0;
			}
			else
				physmem8_ptr = (physmem8_ptr + 1) >> 0;
		}
	}
}