namespace PCEmulator.Net
{
	public abstract class Op : OpContext
	{
		protected Op(CPU_X86_Impl.Executor e) : base(e)
		{
		}

		public abstract void Exec();
	}
}