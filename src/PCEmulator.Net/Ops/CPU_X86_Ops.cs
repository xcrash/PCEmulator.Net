using System;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private readonly JbOpContext Jb;
			private readonly IbContext Ib;
			private readonly IvContext Iv;
			private readonly EvContext Ev;
			private readonly RegsSingleOpContext RegsCtx;
			private readonly SegmentSingleOpContext SegsCtx;

			public Executor()
			{
				Jb = new JbOpContext(this);
				Iv = new IvContext(this);
				Ib = new IbContext(this);
				Ev = new EvContext(this);
				RegsCtx = new RegsSingleOpContext(this);
				SegsCtx = new SegmentSingleOpContext(this);
			}

			public interface IArgumentOperand<T>
			{
				T readX();
				T setX { set; }
			}

			public interface IArgumentOperandCodes<T> : IArgumentOperand<T>
			{
			}

			public class BArgumentOperand : IArgumentOperandCodes<byte>
			{
				private readonly Executor e;

				public BArgumentOperand(Executor e)
				{
					this.e = e;
				}

				public byte readX()
				{
					return e.phys_mem8[e.physmem8_ptr++];
				}

				public byte setX
				{
					set { throw new NotImplementedException(); }
				}
			}

			private class VArgumentOperand : IArgumentOperandCodes<uint>
			{
				private readonly Executor e;

				public VArgumentOperand(Executor e)
				{
					this.e = e;
				}

				public uint readX()
				{
					return e.phys_mem8_uint();
				}

				public uint setX
				{
					set
					{
						var x = value;
						if (e.isRegisterAddressingMode)
						{
							e.regs[regIdx0(e.mem8)] = x;
						}
						else
						{
							e.z = (int)e.regs[4];
							e.segment_translation();
							e.regs[4] = e.y;
							e.st32_mem8_write(x);
							e.regs[4] = (uint)e.z;
						}
					}
				}
			}

			private interface ISpecialArgumentCodes<T> : IArgumentOperand<T>
			{
			}

			private class RegsSpecialArgument : ISpecialArgumentCodes<uint>
			{
				private readonly Executor e;

				public RegsSpecialArgument(Executor e)
				{
					this.e = e;
				}

				public uint readX()
				{
					return e.regs[e.OPbyteRegIdx0];
				}

				public uint setX
				{
					set { e.regs[e.OPbyteRegIdx0] = value; }
				}
			}

			private class SegmentSpecialArgument : ISpecialArgumentCodes<uint>
			{
				private readonly Executor e;

				public SegmentSpecialArgument(Executor e)
				{
					this.e = e;
				}

				private uint segIdx
				{
					get { return e.OPbyte >> 3; }
				}

				public uint readX()
				{
					return (uint)e.cpu.segs[segIdx].selector;
				}

				public uint setX
				{
					set
					{
						var x = value;
						e.set_segment_register((int)(segIdx), (int) x);
						e.pop_dword_from_stack_incr_ptr();
					}
				}
			}

			public class SingleOpContext<T>
			{
				public IArgumentOperand<T> ops { get; set; }

				protected SingleOpContext(IArgumentOperand<T> ops)
				{
					this.ops = ops;
				}

				public T readX()
				{
					return ops.readX();
				}

				public T setX
				{
					set { ops.setX = value; }
				}
			}

			private class EvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public EvContext(Executor e)
					: base(new VArgumentOperand(e))
				{
					this.e = e;
				}
			}

			private class IbContext : SingleOpContext<byte>
			{
				public IbContext(Executor e)
					: base(new BArgumentOperand(e))
				{
				}

				public new uint readX()
				{
					return (uint)((base.readX() << 24) >> 24);
				}
			}

			private class IvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public IvContext(Executor e)
					: base(new VArgumentOperand(e))
				{
					this.e = e;
				}

				public uint readX()
				{
					return ops.readX();
				}
			}

			public class RegsSingleOpContext : SingleOpContext<uint>
			{
				public RegsSingleOpContext(Executor e)
					: base(new RegsSpecialArgument(e))
				{
				}
			}

			public class SegmentSingleOpContext : SingleOpContext<uint>
			{
				public SegmentSingleOpContext(Executor e)
					: base(new SegmentSpecialArgument(e))
				{
				}
			}
		}
	}
}