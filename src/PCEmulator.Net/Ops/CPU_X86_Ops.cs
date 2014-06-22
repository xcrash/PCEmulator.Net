namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private readonly JbOpContext Jb;
			private readonly RegsOpContext RegsCtx;
			private readonly SegmentsContext SegmentsCtx;
			private readonly IbContext Ib;
			private readonly IvContext Iv;
			private readonly EvContext Ev;

			public Executor()
			{
				Jb = new JbOpContext(this);
				RegsCtx = new RegsOpContext(this);
				SegmentsCtx = new SegmentsContext(this);
				Iv = new IvContext(this);
				Ib = new IbContext(this);
				Ev = new EvContext(this);
			}

			public class OpContext
			{
				protected readonly Executor e;

				public OpContext(Executor e)
				{
					this.e = e;
				}
			}

			public interface IArgumentOperand<out T>
			{
				T readX();
			}

			public interface IArgumentOperandCodes<out T> : IArgumentOperand<T>
			{
			}

			public class BOpContext : IArgumentOperandCodes<byte>
			{
				private readonly Executor e;

				public BOpContext(Executor e)
				{
					this.e = e;
				}

				public byte readX()
				{
					return e.phys_mem8[e.physmem8_ptr++];
				}
			}

			private class VOpContext : IArgumentOperandCodes<uint>
			{
				private readonly Executor e;

				public VOpContext(Executor e)
				{
					this.e = e;
				}

				public uint readX()
				{
					return e.phys_mem8_uint();
				}
			}

			public class SingleOpContext<T>
			{
				protected readonly IArgumentOperandCodes<T> ops;

				protected SingleOpContext(IArgumentOperandCodes<T> ops)
				{
					this.ops = ops;
				}
			}
		}
	}
}