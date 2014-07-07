using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	public class SegmentOperand : Operand<uint>
	{
		public SegmentOperand(CPU_X86_Impl.Executor e)
			: base(new SegmentSpecialArgument(e), e)
		{
		}

		public override uint PopValue()
		{
			return e.pop_dword_from_stack_read() & 0xffff;
		}

		public override uint ReadOpValue0()
		{
			throw new System.NotImplementedException();
		}

		public override uint ReadOpValue1()
		{
			throw new System.NotImplementedException();
		}

		public override void ProceedResult(uint r)
		{
			throw new System.NotImplementedException();
		}
	}
}