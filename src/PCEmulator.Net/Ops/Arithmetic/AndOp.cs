using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AndOp : DoubleOperandOp<byte>
	{
		public AndOp(CPU_X86_Impl.Executor e, IOperand<byte> o0, IOperand<byte> o1)
			: base(e, o0, o1)
		{
		}

		protected override uint CalculateResult(uint o0, uint o1)
		{
			var yb = o0;
			yb = (((yb & o1) << 24) >> 24);
			e.u_dst = yb;
			e._op = 12;
			return yb;
		}
	}
}