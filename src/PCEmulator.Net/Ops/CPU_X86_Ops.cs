using PCEmulator.Net.Operands;
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
		}
	}
}