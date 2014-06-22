using System;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private readonly JbOpContext Jb;
			private readonly IbContext Ib;
			private readonly IvContext Iv;
			private readonly EbContext Eb;
			private readonly EvContext Ev;
			private readonly GbContext Gb;
			private readonly RegsSingleOpContext RegsCtx;
			private readonly SegmentSingleOpContext SegsCtx;

			public Executor()
			{
				Jb = new JbOpContext(this);
				Iv = new IvContext(this);
				Ib = new IbContext(this);
				Eb = new EbContext(this);
				Ev = new EvContext(this);
				Gb = new GbContext(this);
				RegsCtx = new RegsSingleOpContext(this);
				SegsCtx = new SegmentSingleOpContext(this);
			}

			public abstract class Op : OpContext
			{
				protected Op(Executor e) : base(e)
				{
				}

				public abstract void Exec();
			}

			private void ExecOp(Op op)
			{
				op.Exec();
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
					set
					{
						var x = value;
						e.st8_mem8_write(x);
					}
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

			public abstract class SingleOpContext<T> : OpContext, IOperand<T>
			{
				public IArgumentOperand<T> ops { get; set; }

				protected SingleOpContext(IArgumentOperand<T> ops, Executor e)
					: base(e)
				{
					this.ops = ops;
				}

				public virtual T readX()
				{
					return ops.readX();
				}
				public virtual T setX
				{
					set { ops.setX = value; }
				}

				public abstract T PopValue();
				public abstract void PushValue(T x);
			}

			public class OpContext
			{
				public readonly Executor e;

				protected int mem8
				{
					//get { return e.mem8; }
					set { e.mem8 = value; }
				}

				protected Uint8Array phys_mem8
				{
					get { return e.phys_mem8; }
				}

				protected Int32Array phys_mem32
				{
					get { return e.phys_mem32; }
				}
				

				protected uint physmem8_ptr
				{
					get { return e.physmem8_ptr; }
					set { e.physmem8_ptr = value; }
				}

				protected uint x
				{
					get { return e.x; }
					set { e.x = value; }
				}

				protected uint y
				{
					set { e.y = value; }
				}

				protected uint[] regs
				{
					get { return e.regs; }
				}

				protected bool FS_usage_flag
				{
					get { return e.FS_usage_flag; }
				}

				protected uint mem8_loc
				{
					get { return e.mem8_loc; }
					set { e.mem8_loc = value; }
				}

				protected int last_tlb_val
				{
					get { return e.last_tlb_val; }
					set { e.last_tlb_val = value; }

				}

				protected int[] _tlb_read_
				{
					get { return e._tlb_read_; }
				}

				protected int[] _tlb_write_
				{
					get { return e._tlb_write_; }
				}

				public OpContext(Executor e)
				{
					this.e = e;
				}
			}

			/// <summary>
			/// E
			/// 
			/// A ModR/M byte follows the opcode and specifies the operand. The operand is either a general-purpose register or a memory address. If it is a memory address, the address is computed from a segment register and any of the following values: a base register, an index register, a displacement.
			/// </summary>
			private class EvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public EvContext(Executor e)
					: base(new VArgumentOperand(e), e)
				{
					this.e = e;
				}

				public override uint PopValue()
				{
					mem8 = phys_mem8[physmem8_ptr++];
					x = e.pop_dword_from_stack_read();
					if (!e.isRegisterAddressingMode)
						y = regs[4];

					e.pop_dword_from_stack_incr_ptr();

					return x;
				}

				public override void PushValue(uint x)
				{
					throw new NotImplementedException();
				}
			}

			private class EbContext : SingleOpContext<byte>
			{
				private readonly Executor e;

				public EbContext(Executor e)
					: base(new BArgumentOperand(e), e)
				{
					this.e = e;
				}

				public int regIdx { get { return regIdx1(e.mem8); } }

				public uint readY()
				{
					return (e.regs[regIdx & 3] >> ((regIdx & 4) << 1));
				}

				public override byte PopValue()
				{
					throw new NotImplementedException();
				}

				public override void PushValue(byte x)
				{
					throw new NotImplementedException();
				}
			}

			/// <summary>
			/// G
			/// 
			/// The reg field of the ModR/M byte selects a general register.
			/// </summary>
			private class GbContext : SingleOpContext<byte>
			{
				private readonly Executor e;

				public GbContext(Executor e)
					: base(new BArgumentOperand(e), e)
				{
					this.e = e;
				}

				public int regIdx { get { return regIdx0(e.mem8); } }

				public void set_word_in_register(uint x)
				{
					e.set_word_in_register(regIdx, x);
				}

				public override byte PopValue()
				{
					throw new NotImplementedException();
				}

				public override void PushValue(byte x)
				{
					throw new NotImplementedException();
				}
			}

			private class IbContext : SingleOpContext<byte>
			{
				public IbContext(Executor e)
					: base(new BArgumentOperand(e), e)
				{
				}

				public override byte PopValue()
				{
					throw new NotImplementedException();
				}

				public override void PushValue(byte _x)
				{
					x = (uint)((_x << 24) >> 24);

					if (FS_usage_flag)
					{
						mem8_loc = (regs[4] - 4) >> 0;
						e.st32_mem8_write(x);
						regs[4] = mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}
			}

			private class IvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public IvContext(Executor e)
					: base(new VArgumentOperand(e), e)
				{
					this.e = e;
				}

				public uint readX()
				{
					return ops.readX();
				}

				public override uint PopValue()
				{
					throw new NotImplementedException();
				}

				public override void PushValue(uint _x)
				{
					x = _x;

					if (FS_usage_flag)
					{
						mem8_loc = (regs[4] - 4) >> 0;
						e.st32_mem8_write(x);
						regs[4] = mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}
			}

			public class RegsSingleOpContext : SingleOpContext<uint>
			{
				public RegsSingleOpContext(Executor e)
					: base(new RegsSpecialArgument(e), e)
				{
				}

				public override uint PopValue()
				{
					if (FS_usage_flag)
					{
						mem8_loc = regs[4];
						x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
							? e.__ld_32bits_mem8_read()
							: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
						regs[4] = (mem8_loc + 4) >> 0;
					}
					else
					{
						x = e.pop_dword_from_stack_read();
						e.pop_dword_from_stack_incr_ptr();
					}
					return x;
				}

				public override void PushValue(uint _x)
				{
					x = _x;

					if (FS_usage_flag)
					{
						mem8_loc = (regs[4] - 4) >> 0;
						last_tlb_val = _tlb_write_[mem8_loc >> 12];
						if (((last_tlb_val | mem8_loc) & 3) != 0)
							e.__st32_mem8_write(x);
						else
							phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;

						regs[4] = mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}
			}

			public class SegmentSingleOpContext : SingleOpContext<uint>
			{
				public SegmentSingleOpContext(Executor e)
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
	}
}