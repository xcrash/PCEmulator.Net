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

		public override void PushValue(uint x)
		{
			e.push_dword_to_stack(x);
		}
	}
}