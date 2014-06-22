using System;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private class SegmentsContext : OpContext
			{
				public SegmentsContext(Executor e) : base(e)
				{
				}

				public uint value
				{
					get { return (uint) e.cpu.segs[segIdx].selector; }
				}

				private uint segIdx
				{
					get { return e.OPbyte >> 3; }
				}

				public void Push(uint x)
				{
					e.push_dword_to_stack(x);
				}

				public uint Pop()
				{
					return (e.pop_dword_from_stack_read() & 0xffff);
				}

				public void set_segment_register(int x)
				{
					e.set_segment_register((int)(segIdx), x);
					e.pop_dword_from_stack_incr_ptr();
				}
			}

			private class IvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public IvContext(Executor e) : base(new VOpContext(e))
				{
					this.e = e;
				}

				public void Push(uint x)
				{
					if (e.FS_usage_flag)
					{
						e.mem8_loc = (e.regs[4] - 4) >> 0;
						e.st32_mem8_write(x);
						e.regs[4] = e.mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}

				public uint readX()
				{
					return ops.readX();
				}
			}

			private class IbContext : SingleOpContext<byte>
			{
				private readonly Executor e;

				public IbContext(Executor e)
					: base(new BOpContext(e))
				{
					this.e = e;
				}

				public uint readUintByteX()
				{
					return (uint)((ops.readX() << 24) >> 24);
				}

				public void Push(uint x)
				{
					if (e.FS_usage_flag)
					{
						e.mem8_loc = (e.regs[4] - 4) >> 0;
						e.st32_mem8_write(x);
						e.regs[4] = e.mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}
			}

			private class EvContext : SingleOpContext<uint>
			{
				private readonly Executor e;

				public EvContext(Executor e) : base(new VOpContext(e))
				{
					this.e = e;
				}

				public uint Pop()
				{
					e.mem8 = e.phys_mem8[e.physmem8_ptr++];
					if (e.isRegisterAddressingMode)
					{
						var x = e.pop_dword_from_stack_read();
						e.pop_dword_from_stack_incr_ptr();
						return x;
					}

					return e.pop_dword_from_stack_read();
				}

				public void Set(uint x)
				{
					if (e.isRegisterAddressingMode)
					{
						e.regs[regIdx0(e.mem8)] = x;
					}
					else
					{
						e.y = e.regs[4];
						e.pop_dword_from_stack_incr_ptr();
						e.z = (int)e.regs[4];
						e.mem8_loc = e.segment_translation(e.mem8);
						e.regs[4] = e.y;
						e.st32_mem8_write(x);
						e.regs[4] = (uint)e.z;
					}
				}
			}

			private void Push(RegsOpContext ctx)
			{
				x = ctx.readX();
				ctx.Push(x);
			}

			private void Push(SegmentsContext ctx)
			{
				ctx.Push(ctx.value);
			}

			private void Push(IvContext ctx)
			{
				x = ctx.readX();
				ctx.Push(x);
			}

			private void Push(IbContext ctx)
			{
				x = ctx.readUintByteX();
				ctx.Push(x);
			}

			private void Pop(RegsOpContext ctx)
			{
				x = ctx.Pop();
				ctx.reg = x;
			}

			private void Pop(SegmentsContext ctx)
			{
				ctx.set_segment_register((int)ctx.Pop());
			}

			private void Pop(EvContext ctx)
			{
				x = ctx.Pop();
				ctx.Set(x);
			}
		}
	}
}