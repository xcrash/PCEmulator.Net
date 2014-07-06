namespace PCEmulator.Net.Operands.Args
{
	public class RegsSpecialArgument : OpContext, ISpecialArgumentCodes<uint>
	{
		public RegsSpecialArgument(CPU_X86_Impl.Executor e) : base(e)
		{
		}

		public uint readX()
		{
			return regs[e.OPbyteRegIdx0];
		}

		public uint setX
		{
			set { regs[e.OPbyteRegIdx0] = value; }
		}
	}
}