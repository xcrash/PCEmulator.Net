using System;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private readonly JbOperand Jb;
			private readonly IbOperand Ib;
			private readonly IvOperand Iv;
			private readonly EbOperand Eb;
			private readonly EvOperand Ev;
			private readonly GbOperand Gb;
			private readonly RegsOperand RegsCtx;
			private readonly SegmentOperand SegsCtx;

			public Executor()
			{
				Jb = new JbOperand(this);
				Iv = new IvOperand(this);
				Ib = new IbOperand(this);
				Eb = new EbOperand(this);
				Ev = new EvOperand(this);
				Gb = new GbOperand(this);
				RegsCtx = new RegsOperand(this);
				SegsCtx = new SegmentOperand(this);
			}

			private void ExecOp(Op op)
			{
				op.Exec();
			}
			
			public interface IOperand<T>
			{
				T readX();
				T setX { set; }

				T PopValue();
				void PushValue(T x);
			}

			public interface IArgumentOperand<T>
			{
				T readX();
				T setX { set; }
			}

			public interface IArgumentOperandCodes<T> : IArgumentOperand<T>
			{
			}

			public class BArgument : OpContext, IArgumentOperandCodes<byte>
			{
				public BArgument(Executor e) : base(e)
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

			private class VArgument : OpContext, IArgumentOperandCodes<uint>
			{
				private readonly Executor e;

				public VArgument(Executor e) : base(e)
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
							z = (int)e.regs[4];
							e.segment_translation();
							regs[4] = y;
							e.st32_mem8_write(x);
							regs[4] = (uint)z;
						}
					}
				}
			}

			private interface ISpecialArgumentCodes<T> : IArgumentOperand<T>
			{
			}

			private class RegsSpecialArgument : OpContext, ISpecialArgumentCodes<uint>
			{
				public RegsSpecialArgument(Executor e) : base(e)
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

			private class SegmentSpecialArgument : OpContext, ISpecialArgumentCodes<uint>
			{
				public SegmentSpecialArgument(Executor e) : base(e)
				{
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

			public abstract class Operand<T> : OpContext, IOperand<T>
			{
				public IArgumentOperand<T> ops { get; set; }

				protected Operand(IArgumentOperand<T> ops, Executor e)
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
					get { return e.y; }
					set { e.y = value; }
				}

				protected int z
				{
					get { return e.z; }
					set { e.z = value; }
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
			private class EvOperand : Operand<uint>
			{
				private readonly Executor e;

				public EvOperand(Executor e)
					: base(new VArgument(e), e)
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

			private class EbOperand : Operand<byte>
			{
				private readonly Executor e;

				public EbOperand(Executor e)
					: base(new BArgument(e), e)
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
			private class GbOperand : Operand<byte>
			{
				private readonly Executor e;

				public GbOperand(Executor e)
					: base(new BArgument(e), e)
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

			private class IbOperand : Operand<byte>
			{
				public IbOperand(Executor e)
					: base(new BArgument(e), e)
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

			private class IvOperand : Operand<uint>
			{
				private readonly Executor e;

				public IvOperand(Executor e)
					: base(new VArgument(e), e)
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

			public class RegsOperand : Operand<uint>
			{
				public RegsOperand(Executor e)
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

			public class SegmentOperand : Operand<uint>
			{
				public SegmentOperand(Executor e)
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