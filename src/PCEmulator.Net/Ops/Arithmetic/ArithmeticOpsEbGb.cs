using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public abstract class ArithmeticOpsEbGb : ArithmeticOpSBase
	{
		protected ArithmeticOpsEbGb(CPU_X86_Impl.Executor e, IOperand<byte> o0, IOperand<byte> o1)
			: base(e, o0, o1)
		{
		}
	}
}