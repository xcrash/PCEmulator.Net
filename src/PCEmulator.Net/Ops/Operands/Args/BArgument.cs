namespace PCEmulator.Net.Operands.Args
{
	public class BArgument : CPU_X86_Impl.Executor.OpContext, IArgumentOperandCodes<byte>
	{
		public BArgument(CPU_X86_Impl.Executor e) : base(e)
		{
		}

		public byte readX()
		{
			return phys_mem8[physmem8_ptr++];
		}

		public byte setX
		{
			set
			{
				var x = value;
				e.st8_mem8_write(x);
			}
		}
	}
}