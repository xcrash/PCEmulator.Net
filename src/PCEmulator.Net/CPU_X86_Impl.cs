using System;
using System.Linq;
using System.Text;
using log4net;
using log4net.Core;
using PCEmulator.Net.IncDec;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl : CPU_X86
	{
		public event Action<string> TestLogEvent;

		private readonly bool isDumpEnabled;
		private int debugLine;

		/* Parity Check by LUT:
				static const bool ParityTable256[256] = {
				#   define P2(n) n, n^1, n^1, n
				#   define P4(n) P2(n), P2(n^1), P2(n^1), P2(n)
				#   define P6(n) P4(n), P4(n^1), P4(n^1), P4(n)
				P6(0), P6(1), P6(1), P6(0) };
				unsigned char b;  // byte value to compute the parity of
				bool parity = ParityTable256[b];
				// OR, for 32-bit words:    unsigned int v; v ^= v >> 16; v ^= v >> 8; bool parity = ParityTable256[v & 0xff];
				// Variation:               unsigned char * p = (unsigned char *) &v; parity = ParityTable256[p[0] ^ p[1] ^ p[2] ^ p[3]];
			*/

		private readonly int[] parity_LUT =
		{
			1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0,
			1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1,
			1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0,
			1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
			1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0,
			1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1,
			1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1
		};

		readonly int[] shift16_LUT = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
		readonly int[] shift8_LUT = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 1, 2, 3, 4 };

		public CPU_X86_Impl(bool isDumpEnabled)
		{
			this.isDumpEnabled = isDumpEnabled;
		}

		public partial class Executor
		{
			internal readonly CPU_X86_Impl cpu;
			internal uint mem8_loc;
			internal uint[] regs;

			internal Uint8Array phys_mem8;
			protected Uint16Array phys_mem16;
			internal Int32Array phys_mem32;

			int[] tlb_read_kernel;
			int[] tlb_write_kernel;
			int[] tlb_read_user;
			int[] tlb_write_user;

			internal int _src;
			internal int _dst;

			internal uint u_src
			{
				get { return (uint)_src; }
				set { _src = (int)value; }
			}

			internal uint u_dst
			{
				get { return (uint)_dst; }
				set { _dst = (int)value; }
			}

			public Int32Array physMem32
			{
				set { phys_mem32 = value; }
				get { return phys_mem32; }
			}

			internal int _op;

			private uint CS_base;
			private uint SS_base;
			private int SS_mask;
			internal bool FS_usage_flag;
			private uint init_CS_flags;
			internal int[] _tlb_write_;
			private uint CS_flags;
			internal int last_tlb_val;
			private readonly ILog opLog = LogManager.GetLogger("OpLogger");

			/*
				  x,y,z,v are either just general non-local values or their exact specialization is unclear,
				  esp. x,y look like they're used for everything

				  I don't know what 'v' should be called, it's not clear yet
				*/
			internal uint x = 0;
			internal uint y;
			internal int z;
			private uint v = 0;

			//note: look like tmp var
			internal int reg_idx1;
			internal uint OPbyte;
			internal int mem8;
			internal int conditional_var;
			internal int reg_idx0;

			public int exec_internal(uint nCycles, IntNoException interrupt)
			{
				N_cycles = nCycles;
				int exit_code;
				int iopl; //io privilege level

				int eip_tlb_val;

				phys_mem8 = cpu.phys_mem8;
				phys_mem16 = cpu.phys_mem16;
				phys_mem32 = cpu.phys_mem32;
				tlb_read_user = cpu.tlb_read_user;
				tlb_write_user = cpu.tlb_write_user;
				tlb_read_kernel = cpu.tlb_read_kernel;
				tlb_write_kernel = cpu.tlb_write_kernel;

				if (cpu.cpl == 3)
				{
					//current privilege level
					_tlb_read_ = tlb_read_user;
					_tlb_write_ = tlb_write_user;
				}
				else
				{
					_tlb_read_ = tlb_read_kernel;
					_tlb_write_ = tlb_write_kernel;
				}

				if (cpu.halted)
				{
					if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
					{
						cpu.halted = false;
					}
					else
					{
						return 257;
					}
				}

				regs = cpu.regs;
				_src = cpu.cc_src;
				_dst = cpu.cc_dst;
				_op = cpu.cc_op;
				_op2 = cpu.cc_op2;
				_dst2 = cpu.cc_dst2;

				eip = cpu.eip;
				init_segment_local_vars();
				exit_code = 256;
				cycles_left = N_cycles;

				if (interrupt != null)
				{
					do_interrupt(interrupt.intno, 0, interrupt.error_code, 0, 0);
				}
				if (cpu.hard_intno >= 0)
				{
					do_interrupt(cpu.hard_intno, 0, 0, 0, 1);
					cpu.hard_intno = -1;
				}
				if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
				{
					cpu.hard_intno = cpu.get_hard_intno();
					do_interrupt(cpu.hard_intno, 0, 0, 0, 1);
					cpu.hard_intno = -1;
				}

				physmem8_ptr = 0;
				initial_mem_ptr = 0;

				#region OUTER_LOOP

			OUTER_LOOP:
				do
				{
					//All the below is solely to determine what the next instruction is before re-entering the main EXEC_LOOP

					eip = (eip + physmem8_ptr - initial_mem_ptr) >> 0;
					eip_offset = (eip + CS_base) >> 0;
					eip_tlb_val = _tlb_read_[eip_offset >> 12];
					if (((eip_tlb_val | eip_offset) & 0xfff) >= (4096 - 15 + 1))
					{
						//what does this condition mean? operation straddling page boundary?
						if (eip_tlb_val == -1)
							do_tlb_set_page(eip_offset, false, cpu.cpl == 3);
						eip_tlb_val = _tlb_read_[eip_offset >> 12];
						initial_mem_ptr = physmem8_ptr = (uint)(eip_offset ^ eip_tlb_val);
						OPbyte = phys_mem8[physmem8_ptr++];
						var Cg = eip_offset & 0xfff;
						if (Cg >= (4096 - 15 + 1))
						{
							//again, WTF does this do?
							x = operation_size_function(eip_offset, OPbyte);
							if ((Cg + x) > 4096)
							{
								initial_mem_ptr = physmem8_ptr = cpu.mem_size;
								for (y = 0; y < x; y++)
								{
									mem8_loc = (eip_offset + y) >> 0;
									phys_mem8[physmem8_ptr + y] = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								physmem8_ptr++;
							}
						}
					}
					else
					{
						initial_mem_ptr = physmem8_ptr = (uint)(eip_offset ^ eip_tlb_val);
						OPbyte = phys_mem8[physmem8_ptr++];
					}

					OPbyte |= (CS_flags = init_CS_flags) & 0x0100;
				//Are we running in 16bit compatibility mode? if so, ops look like 0x1XX instead of 0xXX
				//TODO: implement EXEC_LOOP
				EXEC_LOOP:
					for (; ; )
					{
						cpu.debugLine++;
						DumpOpLog(OPbyte);
						switch (OPbyte)
						{
							case 0x00: //ADD Gb Eb Add
							case 0x02: //ADD Eb Gb Add
							case 0x08: //OR Gb Eb Logical Inclusive OR
							case 0x0a: //OR Eb Gb Logical Inclusive OR
							case 0x10: //ADC Gb Eb Add with Carry
							case 0x12: //ADC Eb Gb Add with Carry
							case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
							case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
							case 0x20: //AND Gb Eb Logical AND
							case 0x22: //AND Eb Gb Logical AND
							case 0x28: //SUB Gb Eb Subtract
							case 0x2a: //SUB Eb Gb Subtract
							case 0x30: //XOR Gb Eb Logical Exclusive OR
							case 0x32: //XOR Eb Gb Logical Exclusive OR
							case 0x38: //CMP Eb  Compare Two Operands
							case 0x3a: //CMP Gb  Compare Two Operands
								ExecOp(BuildOp());
								goto EXEC_LOOP_END;

							case 0x01: //ADD Gvqp Evqp Add
								mem8 = phys_mem8[physmem8_ptr++];
								y = regs[regIdx1(mem8)];
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									{
										u_src = y;
										u_dst = regs[reg_idx0] = (regs[reg_idx0] + u_src) >> 0;
										_op = 2;
									}
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_write();
									{
										u_src = y;
										u_dst = x = (x + u_src) >> 0;
										_op = 2;
									}
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0x03: //ADD Evqp Gvqp Add
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									y = ld_32bits_mem8_read();
								}
								{
									u_src = y;
									u_dst = regs[reg_idx1] = (regs[reg_idx1] + u_src) >> 0;
									_op = 2;
								}
								goto EXEC_LOOP_END;
							case 0x04: //ADD Ib AL Add
							case 0x0c: //OR Ib AL Logical Inclusive OR
							case 0x14: //ADC Ib AL Add with Carry
							case 0x1c: //SBB Ib AL Integer Subtraction with Borrow
							case 0x24: //AND Ib AL Logical AND
							case 0x2c: //SUB Ib AL Subtract
							case 0x34: //XOR Ib AL Logical Exclusive OR
							case 0x3c: //CMP AL  Compare Two Operands
								y = phys_mem8[physmem8_ptr++];
								conditional_var = (int)(OPbyte >> 3);
								set_word_in_register(0, do_8bit_math(conditional_var, regs[REG_AX] & 0xff, y));
								goto EXEC_LOOP_END;
							case 0x05: //ADD Ivds rAX Add
								y = phys_mem8_uint();
								{
									u_src = y;
									u_dst = regs[REG_AX] = (regs[REG_AX] + u_src) >> 0;
									_op = 2;
								}
								goto EXEC_LOOP_END;
							case 0x06://PUSH ES SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
							case 0x0e://PUSH CS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
							case 0x16://PUSH SS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
							case 0x1e://PUSH DS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
								ExecOp(new PushOp<uint>(Operands.SegsCtx));
								goto EXEC_LOOP_END;
							case 0x07: //POP SS:[rSP] ES Pop a Value from the Stack
							case 0x17: //POP SS:[rSP] SS Pop a Value from the Stack
							case 0x1f: //POP SS:[rSP] DS Pop a Value from the Stack
								ExecOp(new PopOp(Operands.SegsCtx));
								//Pop(SegsCtx);
								goto EXEC_LOOP_END;
							case 0x09: //OR Gvqp Evqp Logical Inclusive OR
							case 0x11: //ADC Gvqp Evqp Add with Carry
							case 0x19: //SBB Gvqp Evqp Integer Subtraction with Borrow
							case 0x21: //AND Gvqp Evqp Logical AND
							case 0x29: //SUB Gvqp Evqp Subtract
							case 0x31: //XOR Gvqp Evqp Logical Exclusive OR
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = (int)(OPbyte >> 3);
								y = regs[regIdx1(mem8)];
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_write();
									x = do_32bit_math(conditional_var, x, y);
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0x0b: //OR Evqp Gvqp Logical Inclusive OR
							case 0x13: //ADC Evqp Gvqp Add with Carry
							case 0x1b: //SBB Evqp Gvqp Integer Subtraction with Borrow
							case 0x23: //AND Evqp Gvqp Logical AND
							case 0x2b: //SUB Evqp Gvqp Subtract
							case 0x33: //XOR Evqp Gvqp Logical Exclusive OR
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = (int)(OPbyte >> 3);
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									y = ld_32bits_mem8_read();
								}
								regs[reg_idx1] = do_32bit_math(conditional_var, regs[reg_idx1], y);
								goto EXEC_LOOP_END;
							case 0x0d: //OR Ivds rAX Logical Inclusive OR
							case 0x15: //ADC Ivds rAX Add with Carry
							case 0x1d: //SBB Ivds rAX Integer Subtraction with Borrow
							case 0x25: //AND Ivds rAX Logical AND
							case 0x2d: //SUB Ivds rAX Subtract
								y = phys_mem8_uint();
								conditional_var = (int)(OPbyte >> 3);
								regs[REG_AX] = do_32bit_math(conditional_var, regs[REG_AX], y);
								goto EXEC_LOOP_END;
							case 0x2f: //DAS  AL Decimal Adjust AL after Subtraction
								op_DAS();
								goto EXEC_LOOP_END;
							case 0x35: //XOR Ivds rAX Logical Exclusive OR
								y = phys_mem8_uint();
								{
									u_dst = regs[REG_AX] = regs[REG_AX] ^ y;
									_op = 14;
								}
								goto EXEC_LOOP_END;
							case 0x39: //CMP Evqp  Compare Two Operands
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = (int)(OPbyte >> 3);
								y = regs[regIdx1(mem8)];
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									{
										u_src = y;
										u_dst = (regs[reg_idx0] - u_src) >> 0;
										_op = 8;
									}
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_read();
									{
										u_src = y;
										u_dst = (x - u_src) >> 0;
										_op = 8;
									}
								}
								goto EXEC_LOOP_END;
							case 0x3b: //CMP Gvqp  Compare Two Operands
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = (int)(OPbyte >> 3);
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									y = ld_32bits_mem8_read();
								}
								{
									u_src = y;
									u_dst = (regs[reg_idx1] - u_src) >> 0;
									_op = 8;
								}
								goto EXEC_LOOP_END;
							case 0x3d: //CMP rAX  Compare Two Operands
								y = phys_mem8_uint();
								{
									u_src = y;
									u_dst = (regs[REG_AX] - u_src) >> 0;
									_op = 8;
								}
								goto EXEC_LOOP_END;
							case 0x40: //INC  Zv Increment by 1
							case 0x41: //REX.B   Extension of r/m field, base field, or opcode reg field
							case 0x42: //REX.X   Extension of SIB index field
							case 0x43: //REX.XB   REX.X and REX.B combination
							case 0x44: //REX.R   Extension of ModR/M reg field
							case 0x45: //REX.RB   REX.R and REX.B combination
							case 0x46: //REX.RX   REX.R and REX.X combination
							case 0x47: //REX.RXB   REX.R, REX.X and REX.B combination
								ExecOp(new IncOp(Operands.RegsCtx));
								goto EXEC_LOOP_END;
							case 0x48: //DEC  Zv Decrement by 1
							case 0x49: //REX.WB   REX.W and REX.B combination
							case 0x4a: //REX.WX   REX.W and REX.X combination
							case 0x4b: //REX.WXB   REX.W, REX.X and REX.B combination
							case 0x4c: //REX.WR   REX.W and REX.R combination
							case 0x4d: //REX.WRB   REX.W, REX.R and REX.B combination
							case 0x4e: //REX.WRX   REX.W, REX.R and REX.X combination
							case 0x4f: //REX.WRXB   REX.W, REX.R, REX.X and REX.B combination
								ExecOp(new DecOp(Operands.RegsCtx));
								goto EXEC_LOOP_END;
							case 0x50: //PUSH Zv SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
							case 0x51:
							case 0x52:
							case 0x53:
							case 0x54:
							case 0x55:
							case 0x56:
							case 0x57:
								ExecOp(new PushOp<uint>(Operands.RegsCtx));
								goto EXEC_LOOP_END;
							case 0x58: //POP SS:[rSP] Zv Pop a Value from the Stack
							case 0x59:
							case 0x5a:
							case 0x5b:
							case 0x5c:
							case 0x5d:
							case 0x5e:
							case 0x5f:
								ExecOp(new PopOp(Operands.RegsCtx));
								goto EXEC_LOOP_END;
							case 0x60: //PUSHA AX SS:[rSP] Push All General-Purpose Registers
								op_PUSHA();
								goto EXEC_LOOP_END;
							case 0x61: //POPA SS:[rSP] DI Pop All General-Purpose Registers
								op_POPA();
								goto EXEC_LOOP_END;
							case 0x63: //ARPL Ew  Adjust RPL Field of Segment Selector
								op_ARPL();
								goto EXEC_LOOP_END;
							case 0x64: //FS FS  FS segment override prefix
							case 0x65: //GS GS  GS segment override prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								CS_flags = (uint)((CS_flags & ~0x000f) | ((OPbyteRegIdx0) + 1));
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0x66: //   Operand-size override prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								if ((init_CS_flags & 0x0100) != 0)
									CS_flags = (uint)(CS_flags & ~0x0100);
								else
									CS_flags |= 0x0100;
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0x67: //   Address-size override prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								if ((init_CS_flags & 0x0080) != 0)
									CS_flags = (uint)(CS_flags & ~0x0080);
								else
									CS_flags |= 0x0080;
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0x68: //PUSH Ivs SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
								ExecOp(new PushOp<uint>(Operands.Iv));
								goto EXEC_LOOP_END;
							case 0x69: //IMUL Evqp Gvqp Signed Multiply
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									y = ld_32bits_mem8_read();
								}
								z = (int)(phys_mem8_uint());
								regs[reg_idx1] = op_IMUL32((int) y, (int) z);
								goto EXEC_LOOP_END;
							case 0x6a: //PUSH Ibss SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
								ExecOp(new PushOp<byte>(Operands.Ib));
								goto EXEC_LOOP_END;
							case 0x6b: //IMUL Evqp Gvqp Signed Multiply
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									y = ld_32bits_mem8_read();
								}
								z = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								regs[reg_idx1] = op_IMUL32((int)y, z);
								goto EXEC_LOOP_END;
							case 0x6c: //INS DX (ES:)[rDI] Input from Port to String
								stringOp_INSB();
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0x6f: //OUTS DS:[SI] DX Output String to Port
								stringOp_OUTSD();
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0x70: //JO Jbs  Jump short if overflow (OF=1)
								ExecOp(new JoOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x71: //JNO Jbs  Jump short if not overflow (OF=0)
								ExecOp(new JnoOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x72: //JB Jbs  Jump short if below/not above or equal/carry (CF=1)
								ExecOp(new JbOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x73: //JNB Jbs  Jump short if not below/above or equal/not carry (CF=0)
								ExecOp(new JnbOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x74: //JZ Jbs  Jump short if zero/equal (ZF=0)
								ExecOp(new JzOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x75: //JNZ Jbs  Jump short if not zero/not equal (ZF=1)
								ExecOp(new JnzOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x76: //JBE Jbs  Jump short if below or equal/not above (CF=1 AND ZF=1)
								ExecOp(new JbeOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x77: //JNBE Jbs  Jump short if not below or equal/above (CF=0 AND ZF=0)
								ExecOp(new JnbeOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x78: //JS Jbs  Jump short if sign (SF=1)
								ExecOp(new JsOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x79: //JNS Jbs  Jump short if not sign (SF=0)
								ExecOp(new JnsOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7a: //JP Jbs  Jump short if parity/parity even (PF=1)
								ExecOp(new JpOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7b: //JNP Jbs  Jump short if not parity/parity odd
								ExecOp(new JnpOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7c: //JL Jbs  Jump short if less/not greater (SF!=OF)
								ExecOp(new JlOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7d: //JNL Jbs  Jump short if not less/greater or equal (SF=OF)
								ExecOp(new JnlOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7e: //JLE Jbs  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
								ExecOp(new JleOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x7f: //JNLE Jbs  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
								ExecOp(new JnleOp(Operands.Jb));
								goto EXEC_LOOP_END;
							case 0x80: //ADD Ib Eb Add
							case 0x82: //ADD Ib Eb Add
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									y = phys_mem8[physmem8_ptr++];
									set_word_in_register(reg_idx0, do_8bit_math(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), y));
								}
								else
								{
									segment_translation();
									y = phys_mem8[physmem8_ptr++];
									if (conditional_var != 7)
									{
										x = ld_8bits_mem8_write();
										x = do_8bit_math(conditional_var, x, y);
										st8_mem8_write(x);
									}
									else
									{
										x = ld_8bits_mem8_read();
										do_8bit_math(7, x, y);
									}
								}
								goto EXEC_LOOP_END;
							case 0x81: //ADD Ivds Evqp Add
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (conditional_var == 7)
								{
									if (isRegisterAddressingMode)
									{
										x = regs[regIdx0(mem8)];
									}
									else
									{
										segment_translation();
										x = ld_32bits_mem8_read();
									}
									y = phys_mem8_uint();
									{
										u_src = y;
										u_dst = ((x - u_src) >> 0);
										_op = 8;
									}
								}
								else
								{
									if (isRegisterAddressingMode)
									{
										reg_idx0 = regIdx0(mem8);
										y = phys_mem8_uint();
										regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
									}
									else
									{
										segment_translation();
										y = phys_mem8_uint();
										x = ld_32bits_mem8_write();
										x = do_32bit_math(conditional_var, x, y);
										st32_mem8_write(x);
									}
								}
								goto EXEC_LOOP_END;
							case 0x83: //ADD Ibs Evqp Add
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (conditional_var == 7)
								{
									if (isRegisterAddressingMode)
									{
										x = regs[regIdx0(mem8)];
									}
									else
									{
										segment_translation();
										x = ld_32bits_mem8_read();
									}
									y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
									{
										u_src = y;
										u_dst = ((x - u_src) >> 0);
										_op = 8;
									}
								}
								else
								{
									if (isRegisterAddressingMode)
									{
										reg_idx0 = regIdx0(mem8);
										y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
										regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
									}
									else
									{
										segment_translation();
										y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
										x = ld_32bits_mem8_write();
										x = do_32bit_math(conditional_var, x, y);
										st32_mem8_write(x);
									}
								}
								goto EXEC_LOOP_END;
							case 0x84: //TEST Eb  Logical Compare
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
								}
								else
								{
									segment_translation();
									x = ld_8bits_mem8_read();
								}
								reg_idx1 = regIdx1(mem8);
								y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
								{
									_dst = ((int)((x & y) << 24) >> 24);
									_op = 12;
								}
								goto EXEC_LOOP_END;
							case 0x85: //TEST Evqp  Logical Compare
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									x = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_read();
								}
								y = regs[regIdx1(mem8)];
								{
									u_dst = x & y;
									_op = 14;
								}
								goto EXEC_LOOP_END;
							case 0x87: //XCHG  Gvqp Exchange Register/Memory with Register
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									x = regs[reg_idx0];
									regs[reg_idx0] = regs[reg_idx1];
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_write();
									st32_mem8_write(regs[reg_idx1]);
								}
								regs[reg_idx1] = x;
								goto EXEC_LOOP_END;
							case 0x88: //MOV Gb Eb Move
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								x = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									last_tlb_val = (reg_idx0 & 4) << 1;
									regs[reg_idx0 & 3] = (uint)((regs[reg_idx0 & 3] & ~(0xff << last_tlb_val)) | (((x) & 0xff) << last_tlb_val));
								}
								else
								{
									segment_translation();
									{
										last_tlb_val = _tlb_write_[mem8_loc >> 12];
										if (last_tlb_val == -1)
										{
											__st8_mem8_write((byte)x);
										}
										else
										{
											phys_mem8[mem8_loc ^ last_tlb_val] = (byte)x;
										}
									}
								}
								goto EXEC_LOOP_END;
							case 0x89: //MOV Gvqp Evqp Move
								mem8 = phys_mem8[physmem8_ptr++];
								x = regs[regIdx1(mem8)];
								if (isRegisterAddressingMode)
								{
									regs[regIdx0(mem8)] = x;
								}
								else
								{
									segment_translation();
									{
										last_tlb_val = _tlb_write_[mem8_loc >> 12];
										if (((last_tlb_val | mem8_loc) & 3) != 0)
										{
											__st32_mem8_write(x);
										}
										else
										{
											phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;
										}
									}
								}
								goto EXEC_LOOP_END;
							case 0x8a: //MOV Eb Gb Move
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
								}
								else
								{
									segment_translation();
									x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								reg_idx1 = regIdx1(mem8);
								last_tlb_val = (reg_idx1 & 4) << 1;
								regs[reg_idx1 & 3] = (uint)((regs[reg_idx1 & 3] & ~(0xff << last_tlb_val)) | (((x) & 0xff) << last_tlb_val));
								goto EXEC_LOOP_END;
							case 0x8b: //MOV Evqp Gvqp Move
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									x = regs[regIdx0(mem8)];
								}
								else
								{
									segment_translation();
									x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
										? __ld_32bits_mem8_read()
										: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
								}
								regs[regIdx1(mem8)] = x;
								goto EXEC_LOOP_END;
							case 0x8c: //MOV Sw Mw Move
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (reg_idx1 >= 6)
									abort(6);
								x = (uint)cpu.segs[reg_idx1].selector;
								if (isRegisterAddressingMode)
								{
									if ((((CS_flags >> 8) & 1) ^ 1) != 0)
									{
										regs[regIdx0(mem8)] = x;
									}
									else
									{
										set_lower_word_in_register(regIdx0(mem8), x);
									}
								}
								else
								{
									segment_translation();
									st16_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0x8d: //LEA M Gvqp Load Effective Address
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
									abort(6);
								CS_flags = (uint)((CS_flags & ~0x000f) | (6 + 1));
								regs[regIdx1(mem8)] = segment_translation(mem8);
								goto EXEC_LOOP_END;
							case 0x8e: //MOV Ew Sw Move
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								if (reg_idx1 >= 6 || reg_idx1 == 1)
									abort(6);
								if (isRegisterAddressingMode)
								{
									x = regs[regIdx0(mem8)] & 0xffff;
								}
								else
								{
									segment_translation();
									x = ld_16bits_mem8_read();
								}
								set_segment_register(reg_idx1, (int)x);
								goto EXEC_LOOP_END;
							case 0x8f: //POP SS:[rSP] Ev Pop a Value from the Stack
								ExecOp(new PopOp(Operands.Ev));
								goto EXEC_LOOP_END;
							case 0x90://XCHG  Zvqp Exchange Register/Memory with Register
								goto EXEC_LOOP_END;
							case 0x91: //(90+r)  XCHG  r16/32  eAX     Exchange Register/Memory with Register
							case 0x92:
							case 0x93:
							case 0x94:
							case 0x95:
							case 0x96:
							case 0x97:
								reg_idx1 = (int)(OPbyteRegIdx0);
								x = regs[0];
								regs[0] = regs[reg_idx1];
								regs[reg_idx1] = x;
								goto EXEC_LOOP_END;
							case 0x98: //CBW AL AX Convert Byte to Word
								regs[REG_AX] = (regs[REG_AX] << 16) >> 16;
								goto EXEC_LOOP_END;
							case 0x99: //CWD AX DX Convert Word to Doubleword
								regs[2] = (uint)((int)regs[0] >> 31);
								goto EXEC_LOOP_END;
							case 0x9b: //FWAIT   Check pending unmasked floating-point exceptions
								goto EXEC_LOOP_END;
							case 0x9d: //POPF SS:[rSP] Flags Pop Stack into FLAGS Register
								iopl = (cpu.eflags >> 12) & 3;
								if ((cpu.eflags & 0x00020000) != 0 && iopl != 3)
									abort(13);
								if ((((CS_flags >> 8) & 1) ^ 1) != 0)
								{
									x = pop_dword_from_stack_read();
									pop_dword_from_stack_incr_ptr();
									var tmp = -1;
									y = (uint)tmp;
								}
								else
								{
									x = pop_word_from_stack_read();
									pop_word_from_stack_incr_ptr();
									y = 0xffff;
								}
								z = (0x00000100 | 0x00040000 | 0x00200000 | 0x00004000);
								if (cpu.cpl == 0)
								{
									z |= 0x00000200 | 0x00003000;
								}
								else
								{
									if (cpu.cpl <= iopl)
										z |= 0x00000200;
								}
								set_FLAGS(x, z & y);
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0x9c: //PUSHF Flags SS:[rSP] Push FLAGS Register onto the Stack
								iopl = (cpu.eflags >> 12) & 3;
								if ((cpu.eflags & 0x00020000) != 0 && iopl != 3)
									abort(13);
								x = (uint)(get_FLAGS() & ~(0x00020000 | 0x00010000));
								if ((((CS_flags >> 8) & 1) ^ 1) != 0)
								{
									push_dword_to_stack(x);
								}
								else
								{
									push_word_to_stack(x);
								}
								goto EXEC_LOOP_END;
							case 0xa0: //MOV Ob AL Move byte at (seg:offset) to AL
								mem8_loc = segmented_mem8_loc_for_MOV();
								x = ld_8bits_mem8_read();
								regs[REG_AX] = (uint)((regs[REG_AX] & -256) | x);
								goto EXEC_LOOP_END;
							case 0xa1: //MOV Ovqp rAX Move dword at (seg:offset) to EAX
								mem8_loc = segmented_mem8_loc_for_MOV();
								x = ld_32bits_mem8_read();
								regs[REG_AX] = x;
								goto EXEC_LOOP_END;
							case 0xa2: //MOV AL Ob Move AL to (seg:offset)
								mem8_loc = segmented_mem8_loc_for_MOV();
								st8_mem8_write(regs[REG_AX]);
								goto EXEC_LOOP_END;
							case 0xa3: //MOV rAX Ovqp Move EAX to (seg:offset)
								mem8_loc = segmented_mem8_loc_for_MOV();
								st32_mem8_write(regs[REG_AX]);
								goto EXEC_LOOP_END;
							case 0xa4://MOVS (DS:)[rSI] (ES:)[rDI] Move Data from String to String
								stringOp_MOVSB();
								goto EXEC_LOOP_END;
							case 0xa5: //MOVS DS:[SI] ES:[DI] Move Data from String to String
								stringOp_MOVSD();
								goto EXEC_LOOP_END;
							case 0xa6: //CMPS (ES:)[rDI]  Compare String Operands
								stringOp_CMPSB();
								goto EXEC_LOOP_END;
							case 0xa7: //CMPS ES:[DI]  Compare String Operands
								stringOp_CMPSD();
								goto EXEC_LOOP_END;
							case 0xa8: //TEST AL  Logical Compare
								y = phys_mem8[physmem8_ptr++];
								{
									_dst = ((int)((regs[REG_AX] & y) << 24) >> 24);
									_op = 12;
								}
								goto EXEC_LOOP_END;
							case 0xa9: //TEST rAX  Logical Compare
								y = phys_mem8_uint();
								{
									u_dst = regs[REG_AX] & y;
									_op = 14;
								}
								goto EXEC_LOOP_END;
							case 0xaa: //STOS AL (ES:)[rDI] Store String
								stringOp_STOSB();
								goto EXEC_LOOP_END;
							case 0xab: //STOS AX ES:[DI] Store String
								stringOp_STOSD();
								goto EXEC_LOOP_END;
							case 0xac: //LODS (DS:)[rSI] AL Load String
								stringOp_LODSB();
								goto EXEC_LOOP_END;
							case 0xae: //SCAS (ES:)[rDI]  Scan String
								stringOp_SCASB();
								goto EXEC_LOOP_END;
							case 0xaf: //SCAS ES:[DI]  Scan String
								stringOp_SCASD();
								goto EXEC_LOOP_END;
							case 0xb0: //MOV Ib Zb Move
							case 0xb1:
							case 0xb2:
							case 0xb3:
							case 0xb4:
							case 0xb5:
							case 0xb6:
							case 0xb7:
								x = phys_mem8[physmem8_ptr++]; //r8
								OPbyte &= 7; //last bits
								last_tlb_val = (int)((OPbyte & 4) << 1);
								regs[OPbyte & 3] = (uint)((regs[OPbyte & 3] & ~(0xff << last_tlb_val)) | (((x) & 0xff) << last_tlb_val));
								goto EXEC_LOOP_END;
							case 0xb8: //MOV Ivqp Zvqp Move
							case 0xb9:
							case 0xba:
							case 0xbb:
							case 0xbc:
							case 0xbd:
							case 0xbe:
							case 0xbf:
								x = phys_mem8_uint();
								regs[OPbyteRegIdx0] = x;
								goto EXEC_LOOP_END;
							case 0xc0: //ROL Ib Eb Rotate
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = phys_mem8[physmem8_ptr++];
									reg_idx0 = regIdx0(mem8);
									set_word_in_register(reg_idx0, shift8(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), (int)y));
								}
								else
								{
									segment_translation();
									y = phys_mem8[physmem8_ptr++];
									x = ld_8bits_mem8_write();
									x = shift8(conditional_var, x, (int)y);
									st8_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xc1: //ROL Ib Evqp Rotate
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									y = phys_mem8[physmem8_ptr++];
									reg_idx0 = regIdx0(mem8);
									regs[reg_idx0] = shift32(conditional_var, regs[reg_idx0], (int)y);
								}
								else
								{
									segment_translation();
									y = phys_mem8[physmem8_ptr++];
									x = ld_32bits_mem8_write();
									x = shift32(conditional_var, x, (int)y);
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xc2: //RETN SS:[rSP]  Return from procedure
								y = (uint)((ld16_mem8_direct() << 16) >> 16);
								x = pop_dword_from_stack_read();
								regs[4] = (uint)((regs[4] & ~SS_mask) | ((regs[4] + 4 + y) & SS_mask));
								eip = x;
								physmem8_ptr = initial_mem_ptr = 0;
								goto EXEC_LOOP_END;
							case 0xc3: //RETN SS:[rSP]  Return from procedure
								if (FS_usage_flag)
								{
									mem8_loc = regs[4];
									x = ld_32bits_mem8_read();
									regs[4] = (regs[4] + 4) >> 0;
								}
								else
								{
									x = pop_dword_from_stack_read();
									pop_dword_from_stack_incr_ptr();
								}
								eip = x;
								physmem8_ptr = initial_mem_ptr = 0;
								goto EXEC_LOOP_END;
							case 0xc6: //MOV Ib Eb Move
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									x = phys_mem8[physmem8_ptr++];
									set_word_in_register(regIdx0(mem8), x);
								}
								else
								{
									segment_translation();
									x = phys_mem8[physmem8_ptr++];
									st8_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xc7: //MOV Ivds Evqp Move
								mem8 = phys_mem8[physmem8_ptr++];
								if (isRegisterAddressingMode)
								{
									x = phys_mem8_uint();
									regs[regIdx0(mem8)] = x;
								}
								else
								{
									segment_translation();
									x = phys_mem8_uint();
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xc9: //LEAVE SS:[rSP] eBP High Level Procedure Exit
								if (FS_usage_flag)
								{
									mem8_loc = regs[5];
									x = ld_32bits_mem8_read();
									regs[5] = x;
									regs[4] = (mem8_loc + 4) >> 0;
								}
								else
								{
									op_LEAVE();
								}
								goto EXEC_LOOP_END;
							case 0xcd: //INT Ib SS:[rSP] Call to Interrupt Procedure
								x = phys_mem8[physmem8_ptr++];
								if ((cpu.eflags & 0x00020000) != 0 && ((cpu.eflags >> 12) & 3) != 3)
									abort(13);
								y = (eip + physmem8_ptr - initial_mem_ptr);
								do_interrupt((int)x, 1, 0, (int)y, 0);
								goto EXEC_LOOP_END;
							case 0xcf: //IRET SS:[rSP] Flags Interrupt Return
								op_IRET((((CS_flags >> 8) & 1) ^ 1));
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xd0: //ROL 1 Eb Rotate
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									set_word_in_register(reg_idx0, shift8(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), 1));
								}
								else
								{
									segment_translation();
									x = ld_8bits_mem8_write();
									x = shift8(conditional_var, x, 1);
									st8_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xd1: //ROL 1 Evqp Rotate
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									regs[reg_idx0] = shift32(conditional_var, regs[reg_idx0], 1);
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_write();
									x = shift32(conditional_var, x, 1);
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xd3: //ROL CL Evqp Rotate
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								y = regs[1] & 0xff;
								if (isRegisterAddressingMode)
								{
									reg_idx0 = regIdx0(mem8);
									regs[reg_idx0] = shift32(conditional_var, regs[reg_idx0], (int)y);
								}
								else
								{
									segment_translation();
									x = ld_32bits_mem8_write();
									x = shift32(conditional_var, x, (int)y);
									st32_mem8_write(x);
								}
								goto EXEC_LOOP_END;
							case 0xd8: //FADD Msr ST Add
							case 0xd9: //FLD ESsr ST Load Floating Point Value
							case 0xda: //FIADD Mdi ST Add
							case 0xdb: //FILD Mdi ST Load Integer
							case 0xdc: //FADD Mdr ST Add
							case 0xdd: //FLD Mdr ST Load Floating Point Value
							case 0xde: //FIADD Mwi ST Add
							case 0xdf: //FILD Mwi ST Load Integer
								if ((cpu.cr0 & ((1 << 2) | (1 << 3))) != 0)
								{
									abort(7);
								}
								mem8 = phys_mem8[physmem8_ptr++];
								reg_idx1 = regIdx1(mem8);
								reg_idx0 = regIdx0(mem8);
								conditional_var = (int)((OPbyteRegIdx0 << 3) | (regIdx1(mem8)));
								set_lower_word_in_register(0, 0xffff);
								if (isRegisterAddressingMode)
								{
								}
								else
								{
									segment_translation();
								}
								goto EXEC_LOOP_END;
							case 0xe0: //LOOPNZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=0
							case 0xe1: //LOOPZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=1
							case 0xe2: //LOOP Jbs eCX Decrement count; Jump short if count!=0
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								if ((CS_flags & 0x0080) != 0)
									conditional_var = 0xffff;
								else
									conditional_var = -1;
								y = (uint)((regs[REG_CX] - 1) & conditional_var);
								regs[REG_CX] = (uint)((regs[REG_CX] & ~conditional_var) | y);
								OPbyte &= 3;
								if (OPbyte == 0)
									z = u_dst != 0 ? 1 : 0;
								else if (OPbyte == 1)
									z = (u_dst == 0) ? 1 : 0;
								else
									z = 1;
								if (y != 0 && z != 0)
								{
									if ((CS_flags & 0x0100) != 0)
									{
										eip = (eip + physmem8_ptr - initial_mem_ptr + x) & 0xffff;
										physmem8_ptr = initial_mem_ptr = 0;
									}
									else
									{
										physmem8_ptr = (physmem8_ptr + x) >> 0;
									}
								}
								goto EXEC_LOOP_END;
							case 0xe4: //IN Ib AL Input from Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								x = phys_mem8[physmem8_ptr++];
								set_word_in_register(0, cpu.ld8_port(x));
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xe6: //OUT AL Ib Output to Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								x = phys_mem8[physmem8_ptr++];
								cpu.st8_port(x, (byte)regs[0]);
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xe7: //OUT eAX Ib Output to Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								x = phys_mem8[physmem8_ptr++];
								cpu.st32_port(x, regs[0]);
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xe8: //CALL Jvds SS:[rSP] Call Procedure
								x = phys_mem8_uint();
								y = (eip + physmem8_ptr - initial_mem_ptr);
								if (FS_usage_flag)
								{
									mem8_loc = (regs[4] - 4) >> 0;
									st32_mem8_write(y);
									regs[4] = mem8_loc;
								}
								else
								{
									push_dword_to_stack(y);
								}
								physmem8_ptr = (physmem8_ptr + x) >> 0;
								goto EXEC_LOOP_END;
							case 0xea: //JMPF Ap  Jump
								if ((((CS_flags >> 8) & 1) ^ 1) != 0)
								{
									x = phys_mem8_uint();
								}
								else
								{
									x = (uint)ld16_mem8_direct();
								}
								y = (uint)ld16_mem8_direct();
								op_JMPF(y, x);
								goto EXEC_LOOP_END;
							case 0xeb: //JMP Jbs  Jump
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
								goto EXEC_LOOP_END;
							case 0xec: //IN DX AL Input from Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								set_word_in_register(0, cpu.ld8_port(regs[2] & 0xffff));
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xed: //IN DX eAX Input from Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								regs[0] = (uint)(short)cpu.ld32_port(regs[2] & 0xffff);
							{
								if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
									goto OUTER_LOOP_END;
							}
								goto EXEC_LOOP_END;
							case 0xee: //OUT AL DX Output to Port
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								cpu.st8_port(regs[2] & 0xffff, (byte)regs[REG_AX]);
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xe9: //JMP Jvds  Jump
								x = phys_mem8_uint();
								physmem8_ptr = (physmem8_ptr + x) >> 0;
								goto EXEC_LOOP_END;
							case 0xf0://LOCK   Assert LOCK# Signal Prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								CS_flags |= 0x0040;
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0xf2://REPNZ  eCX Repeat String Operation Prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								CS_flags |= 0x0020;
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0xf3: //REPZ  eCX Repeat String Operation Prefix
								if (CS_flags == init_CS_flags)
									operation_size_function(eip_offset, OPbyte);
								CS_flags |= 0x0010;
								OPbyte = phys_mem8[physmem8_ptr++];
								OPbyte |= (CS_flags & 0x0100);
								break;
							case 0xf4: //HLT   Halt
								if (cpu.cpl != 0)
									abort(13);
								cpu.halted = true;
								exit_code = 257;
								goto OUTER_LOOP_END;
							case 0xf6: //TEST Eb  Logical Compare
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								switch (conditional_var)
								{
									case 0:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_read();
										}
										y = phys_mem8[physmem8_ptr++];
										{
											_dst = (((int)(x & y) << 24) >> 24);
											_op = 12;
										}
										break;
									case 2:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											set_word_in_register(reg_idx0, ~(regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_write();
											x = ~x;
											st8_mem8_write(x);
										}
										break;
									case 3:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											set_word_in_register(reg_idx0, do_8bit_math(5, 0, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1))));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_write();
											x = do_8bit_math(5, 0, x);
											st8_mem8_write(x);
										}
										break;
									case 4:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_read();
										}
										set_lower_word_in_register(0, op_MUL(regs[0], x));
										break;
									case 5:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_read();
										}
										set_lower_word_in_register(0, op_IMUL(regs[0], x));
										break;
									case 6:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_read();
										}
										op_DIV(x);
										break;
									case 7:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_read();
										}
										op_IDIV(x);
										break;
									default:
										abort(6);
										break;
								}
								goto EXEC_LOOP_END;
							case 0xf7: //TEST Evqp  Logical Compare
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								switch (conditional_var)
								{
									case 0:
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										y = phys_mem8_uint();
										{
											u_dst = (x & y);
											_op = 14;
										}
										break;
									case 2:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = ~regs[reg_idx0];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											x = ~x;
											st32_mem8_write(x);
										}
										break;
									case 3:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = do_32bit_math(5, 0, regs[reg_idx0]);
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											x = do_32bit_math(5, 0, x);
											st32_mem8_write(x);
										}
										break;
									case 4:
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										regs[0] = (uint)op_MUL32(regs[0], x);
										regs[2] = v;
										break;
									case 5:
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										regs[0] = op_IMUL32((int)regs[0], (int)x);
										regs[2] = v;
										break;
									case 6:
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										regs[0] = op_DIV32(regs[2], regs[0], x);
										regs[2] = v;
										break;
									case 7:
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										regs[0] = op_IDIV32((int)regs[2], regs[0], (int)x);
										regs[2] = v;
										break;
									default:
										abort(6);
										break;
								}
								goto EXEC_LOOP_END;
							case 0xfa: //CLI   Clear Interrupt Flag
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								cpu.eflags &= ~0x00000200;
								goto EXEC_LOOP_END;
							case 0xfb: //STI   Set Interrupt Flag
								iopl = (cpu.eflags >> 12) & 3;
								if (cpu.cpl > iopl)
									abort(13);
								cpu.eflags |= 0x00000200;
								{
									if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
										goto OUTER_LOOP_END;
								}
								goto EXEC_LOOP_END;
							case 0xfc: //CLD   Clear Direction Flag
								cpu.df = 1;
								goto EXEC_LOOP_END;
							case 0xfd: //STD   Set Direction Flag
								cpu.df = -1;
								goto EXEC_LOOP_END;
							case 0xfe: //INC  Eb Increment by 1
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								switch (conditional_var)
								{
									case 0:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											set_word_in_register(reg_idx0, (uint)increment_8bit((regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1))));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_write();
											x = (uint)increment_8bit(x);
											st8_mem8_write(x);
										}
										break;
									case 1:
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											set_word_in_register(reg_idx0, (uint)decrement_8bit((regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1))));
										}
										else
										{
											segment_translation();
											x = ld_8bits_mem8_write();
											x = (uint)decrement_8bit(x);
											st8_mem8_write(x);
										}
										break;
									default:
										abort(6);
										break;
								}
								goto EXEC_LOOP_END;

							case 0xff: //INC DEC CALL CALLF JMP JMPF PUSH
								mem8 = phys_mem8[physmem8_ptr++];
								conditional_var = regIdx1(mem8);
								switch (conditional_var)
								{
									case 0: //INC  Evqp Increment by 1
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											{
												if (_op < 25)
												{
													_op2 = _op;
													_dst2 = (int)u_dst;
												}
												regs[reg_idx0] = u_dst = (regs[reg_idx0] + 1) >> 0;
												_op = 27;
											}
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											{
												if (_op < 25)
												{
													_op2 = _op;
													_dst2 = (int)u_dst;
												}
												x = u_dst = (x + 1) >> 0;
												_op = 27;
											}
											st32_mem8_write(x);
										}
										break;
									case 1: //DEC
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											{
												if (_op < 25)
												{
													_op2 = _op;
													_dst2 = (int)u_dst;
												}
												regs[reg_idx0] = u_dst = (regs[reg_idx0] - 1) >> 0;
												_op = 30;
											}
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											{
												if (_op < 25)
												{
													_op2 = _op;
													_dst2 = (int)u_dst;
												}
												x = u_dst = (x - 1) >> 0;
												_op = 30;
											}
											st32_mem8_write(x);
										}
										break;
									case 2: //CALL
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										y = (eip + physmem8_ptr - initial_mem_ptr);
										if (FS_usage_flag)
										{
											mem8_loc = (regs[4] - 4) >> 0;
											st32_mem8_write(y);
											regs[4] = mem8_loc;
										}
										else
										{
											push_dword_to_stack(y);
										}
										eip = x;
										physmem8_ptr = initial_mem_ptr = 0;
										break;
									case 4: //JMP
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										eip = x;
										physmem8_ptr = initial_mem_ptr = 0;
										break;
									case 6: //PUSH
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_read();
										}
										if (FS_usage_flag)
										{
											mem8_loc = (regs[4] - 4) >> 0;
											st32_mem8_write(x);
											regs[4] = mem8_loc;
										}
										else
										{
											push_dword_to_stack(x);
										}
										break;
									case 3: //CALLF
									case 5: //JMPF
										if (isRegisterAddressingMode)
											abort(6);
										segment_translation();
										x = ld_32bits_mem8_read();
										mem8_loc = (mem8_loc + 4) >> 0;
										y = ld_16bits_mem8_read();
										if (conditional_var == 3)
											op_CALLF(1, y, x, (eip + physmem8_ptr - initial_mem_ptr));
										else
											op_JMPF(y, x);
										break;
									default:
										abort(6);
										break;
								}
								goto EXEC_LOOP_END;

							/*
						TWO BYTE CODE INSTRUCTIONS BEGIN WITH 0F :  0F xx
						=====================================================================================================
						*/
							case 0x0f:
								OPbyte = phys_mem8[physmem8_ptr++];
								switch (OPbyte)
								{
									case 0x00: //SLDT
										if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
											abort(6);
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										switch (conditional_var)
										{
											case 0: //SLDT Store Local Descriptor Table Register
											case 1: //STR Store Task Register
												if (conditional_var == 0)
													x = (uint)cpu.ldt.selector;
												else
													x = (uint)cpu.tr.selector;
												if (isRegisterAddressingMode)
												{
													set_lower_word_in_register(regIdx0(mem8), x);
												}
												else
												{
													segment_translation();
													st16_mem8_write(x);
												}
												break;
											case 2: //LDTR Load Local Descriptor Table Register
											case 3: //LTR Load Task Register
												if (cpu.cpl != 0)
													abort(13);
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)] & 0xffff;
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												if (conditional_var == 2)
													op_LDTR(x);
												else
													op_LTR((int)x);
												break;
											case 4: //VERR Verify a Segment for Reading
											case 5: //VERW Verify a Segment for Writing
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)] & 0xffff;
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												op_VERR_VERW(x, conditional_var & 1);
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;
									case 0x01: //SGDT GDTR Ms Store Global Descriptor Table Register
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										switch (conditional_var)
										{
											case 2:
											case 3:
												if (isRegisterAddressingMode)
													abort(6);
												if (cpu.cpl != 0)
													abort(13);
												segment_translation();
												x = ld_16bits_mem8_read();
												mem8_loc += 2;
												y = ld_32bits_mem8_read();
												if (conditional_var == 2)
												{
													cpu.gdt.@base = y;
													cpu.gdt.limit = (int)x;
												}
												else
												{
													cpu.idt.@base = y;
													cpu.idt.limit = (int)x;
												}
												break;
											case 7:
												if (cpu.cpl != 0)
													abort(13);
												if (isRegisterAddressingMode)
													abort(6);
												segment_translation();
												tlb_flush_page(mem8_loc & -4096);
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;
									case 0x02: //LAR Mw Gvqp Load Access Rights Byte
									case 0x03: //LSL Mw Gvqp Load Segment Limit
										op_LAR_LSL((((CS_flags >> 8) & 1) ^ 1), OPbyte & 1);
										goto EXEC_LOOP_END;
									case 0x04:
									case 0x05://LOADALL  AX Load All of the CPU Registers
									case 0x07://LOADALL  EAX Load All of the CPU Registers
									case 0x08://INVD   Invalidate Internal Caches
									case 0x09://WBINVD   Write Back and Invalidate Cache
									case 0x0a:
									case 0x0b://UD2   Undefined Instruction
									case 0x0c:
									case 0x0d://NOP Ev  No Operation
									case 0x0e:
									case 0x0f:
									case 0x10://MOVUPS Wps Vps Move Unaligned Packed Single-FP Values
									case 0x11://MOVUPS Vps Wps Move Unaligned Packed Single-FP Values
									case 0x12://MOVHLPS Uq Vq Move Packed Single-FP Values High to Low
									case 0x13://MOVLPS Vq Mq Move Low Packed Single-FP Values
									case 0x14://UNPCKLPS Wq Vps Unpack and Interleave Low Packed Single-FP Values
									case 0x15://UNPCKHPS Wq Vps Unpack and Interleave High Packed Single-FP Values
									case 0x16://MOVLHPS Uq Vq Move Packed Single-FP Values Low to High
									case 0x17://MOVHPS Vq Mq Move High Packed Single-FP Values
									case 0x18://HINT_NOP Ev  Hintable NOP
									case 0x19://HINT_NOP Ev  Hintable NOP
									case 0x1a://HINT_NOP Ev  Hintable NOP
									case 0x1b://HINT_NOP Ev  Hintable NOP
									case 0x1c://HINT_NOP Ev  Hintable NOP
									case 0x1d://HINT_NOP Ev  Hintable NOP
									case 0x1e://HINT_NOP Ev  Hintable NOP
									case 0x1f://HINT_NOP Ev  Hintable NOP
									case 0x21://MOV Dd Rd Move to/from Debug Registers
									case 0x24://MOV Td Rd Move to/from Test Registers
									case 0x25:
									case 0x26://MOV Rd Td Move to/from Test Registers
									case 0x27:
									case 0x28://MOVAPS Wps Vps Move Aligned Packed Single-FP Values
									case 0x29://MOVAPS Vps Wps Move Aligned Packed Single-FP Values
									case 0x2a://CVTPI2PS Qpi Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
									case 0x2b://MOVNTPS Vps Mps Store Packed Single-FP Values Using Non-Temporal Hint
									case 0x2c://CVTTPS2PI Wpsq Ppi Convert with Trunc. Packed Single-FP Values to1.11 PackedDW Integers
									case 0x2d://CVTPS2PI Wpsq Ppi Convert Packed Single-FP Values to1.11 PackedDW Integers
									case 0x2e://UCOMISS Vss  Unordered Compare Scalar Single-FP Values and Set EFLAGS
									case 0x2f://COMISS Vss  Compare Scalar Ordered Single-FP Values and Set EFLAGS
									case 0x30://WRMSR rCX MSR Write to Model Specific Register
									case 0x32://RDMSR rCX rAX Read from Model Specific Register
									case 0x33://RDPMC PMC EAX Read Performance-Monitoring Counters
									case 0x34://SYSENTER IA32_SYSENTER_CS SS Fast System Call
									case 0x35://SYSEXIT IA32_SYSENTER_CS SS Fast Return from Fast System Call
									case 0x36:
									case 0x37://GETSEC EAX  GETSEC Leaf Functions
									case 0x38://PSHUFB Qq Pq Packed Shuffle Bytes
									case 0x39:
									case 0x3a://ROUNDPS Wps Vps Round Packed Single-FP Values
									case 0x3b:
									case 0x3c:
									case 0x3d:
									case 0x3e:
									case 0x3f:
									case 0x50://MOVMSKPS Ups Gdqp Extract Packed Single-FP Sign Mask
									case 0x51://SQRTPS Wps Vps Compute Square Roots of Packed Single-FP Values
									case 0x52://RSQRTPS Wps Vps Compute Recipr. of Square Roots of Packed Single-FP Values
									case 0x53://RCPPS Wps Vps Compute Reciprocals of Packed Single-FP Values
									case 0x54://ANDPS Wps Vps Bitwise Logical AND of Packed Single-FP Values
									case 0x55://ANDNPS Wps Vps Bitwise Logical AND NOT of Packed Single-FP Values
									case 0x56://ORPS Wps Vps Bitwise Logical OR of Single-FP Values
									case 0x57://XORPS Wps Vps Bitwise Logical XOR for Single-FP Values
									case 0x58://ADDPS Wps Vps Add Packed Single-FP Values
									case 0x59://MULPS Wps Vps Multiply Packed Single-FP Values
									case 0x5a://CVTPS2PD Wps Vpd Convert Packed Single-FP Values to1.11 PackedDouble-FP Values
									case 0x5b://CVTDQ2PS Wdq Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
									case 0x5c://SUBPS Wps Vps Subtract Packed Single-FP Values
									case 0x5d://MINPS Wps Vps Return Minimum Packed Single-FP Values
									case 0x5e://DIVPS Wps Vps Divide Packed Single-FP Values
									case 0x5f://MAXPS Wps Vps Return Maximum Packed Single-FP Values
									case 0x60://PUNPCKLBW Qd Pq Unpack Low Data
									case 0x61://PUNPCKLWD Qd Pq Unpack Low Data
									case 0x62://PUNPCKLDQ Qd Pq Unpack Low Data
									case 0x63://PACKSSWB Qd Pq Pack with Signed Saturation
									case 0x64://PCMPGTB Qd Pq Compare Packed Signed Integers for Greater Than
									case 0x65://PCMPGTW Qd Pq Compare Packed Signed Integers for Greater Than
									case 0x66://PCMPGTD Qd Pq Compare Packed Signed Integers for Greater Than
									case 0x67://PACKUSWB Qq Pq Pack with Unsigned Saturation
									case 0x68://PUNPCKHBW Qq Pq Unpack High Data
									case 0x69://PUNPCKHWD Qq Pq Unpack High Data
									case 0x6a://PUNPCKHDQ Qq Pq Unpack High Data
									case 0x6b://PACKSSDW Qq Pq Pack with Signed Saturation
									case 0x6c://PUNPCKLQDQ Wdq Vdq Unpack Low Data
									case 0x6d://PUNPCKHQDQ Wdq Vdq Unpack High Data
									case 0x6e://MOVD Ed Pq Move Doubleword
									case 0x6f://MOVQ Qq Pq Move Quadword
									case 0x70://PSHUFW Qq Pq Shuffle Packed Words
									case 0x71://PSRLW Ib Nq Shift Packed Data Right Logical
									case 0x72://PSRLD Ib Nq Shift Double Quadword Right Logical
									case 0x73://PSRLQ Ib Nq Shift Packed Data Right Logical
									case 0x74://PCMPEQB Qq Pq Compare Packed Data for Equal
									case 0x75://PCMPEQW Qq Pq Compare Packed Data for Equal
									case 0x76://PCMPEQD Qq Pq Compare Packed Data for Equal
									case 0x77://EMMS   Empty MMX Technology State
									case 0x78://VMREAD Gd Ed Read Field from Virtual-Machine Control Structure
									case 0x79://VMWRITE Gd  Write Field to Virtual-Machine Control Structure
									case 0x7a:
									case 0x7b:
									case 0x7c://HADDPD Wpd Vpd Packed Double-FP Horizontal Add
									case 0x7d://HSUBPD Wpd Vpd Packed Double-FP Horizontal Subtract
									case 0x7e://MOVD Pq Ed Move Doubleword
									case 0x7f://MOVQ Pq Qq Move Quadword
									case 0xa6:
									case 0xa7:
									case 0xaa://RSM  Flags Resume from System Management Mode
									case 0xae://FXSAVE ST Mstx Save x87 FPU, MMX, XMM, and MXCSR State
									case 0xb8://JMPE   Jump to IA-64 Instruction Set
									case 0xb9://UD G  Undefined Instruction
									case 0xc2://CMPPS Wps Vps Compare Packed Single-FP Values
									case 0xc3://MOVNTI Gdqp Mdqp Store Doubleword Using Non-Temporal Hint
									case 0xc4://PINSRW Rdqp Pq Insert Word
									case 0xc5://PEXTRW Nq Gdqp Extract Word
									case 0xc6://SHUFPS Wps Vps Shuffle Packed Single-FP Values
									case 0xc7://CMPXCHG8B EBX Mq Compare and Exchange Bytes
									case 0xd0://ADDSUBPD Wpd Vpd Packed Double-FP Add/Subtract
									case 0xd1://PSRLW Qq Pq Shift Packed Data Right Logical
									case 0xd2://PSRLD Qq Pq Shift Packed Data Right Logical
									case 0xd3://PSRLQ Qq Pq Shift Packed Data Right Logical
									case 0xd4://PADDQ Qq Pq Add Packed Quadword Integers
									case 0xd5://PMULLW Qq Pq Multiply Packed Signed Integers and Store Low Result
									case 0xd6://MOVQ Vq Wq Move Quadword
									case 0xd7://PMOVMSKB Nq Gdqp Move Byte Mask
									case 0xd8://PSUBUSB Qq Pq Subtract Packed Unsigned Integers with Unsigned Saturation
									case 0xd9://PSUBUSW Qq Pq Subtract Packed Unsigned Integers with Unsigned Saturation
									case 0xda://PMINUB Qq Pq Minimum of Packed Unsigned Byte Integers
									case 0xdb://PAND Qd Pq Logical AND
									case 0xdc://PADDUSB Qq Pq Add Packed Unsigned Integers with Unsigned Saturation
									case 0xdd://PADDUSW Qq Pq Add Packed Unsigned Integers with Unsigned Saturation
									case 0xde://PMAXUB Qq Pq Maximum of Packed Unsigned Byte Integers
									case 0xdf://PANDN Qq Pq Logical AND NOT
									case 0xe0://PAVGB Qq Pq Average Packed Integers
									case 0xe1://PSRAW Qq Pq Shift Packed Data Right Arithmetic
									case 0xe2://PSRAD Qq Pq Shift Packed Data Right Arithmetic
									case 0xe3://PAVGW Qq Pq Average Packed Integers
									case 0xe4://PMULHUW Qq Pq Multiply Packed Unsigned Integers and Store High Result
									case 0xe5://PMULHW Qq Pq Multiply Packed Signed Integers and Store High Result
									case 0xe6://CVTPD2DQ Wpd Vdq Convert Packed Double-FP Values to1.11 PackedDW Integers
									case 0xe7://MOVNTQ Pq Mq Store of Quadword Using Non-Temporal Hint
									case 0xe8://PSUBSB Qq Pq Subtract Packed Signed Integers with Signed Saturation
									case 0xe9://PSUBSW Qq Pq Subtract Packed Signed Integers with Signed Saturation
									case 0xea://PMINSW Qq Pq Minimum of Packed Signed Word Integers
									case 0xeb://POR Qq Pq Bitwise Logical OR
									case 0xec://PADDSB Qq Pq Add Packed Signed Integers with Signed Saturation
									case 0xed://PADDSW Qq Pq Add Packed Signed Integers with Signed Saturation
									case 0xee://PMAXSW Qq Pq Maximum of Packed Signed Word Integers
									case 0xef://PXOR Qq Pq Logical Exclusive OR
									case 0xf0://LDDQU Mdq Vdq Load Unaligned Integer 128 Bits
									case 0xf1://PSLLW Qq Pq Shift Packed Data Left Logical
									case 0xf2://PSLLD Qq Pq Shift Packed Data Left Logical
									case 0xf3://PSLLQ Qq Pq Shift Packed Data Left Logical
									case 0xf4://PMULUDQ Qq Pq Multiply Packed Unsigned DW Integers
									case 0xf5://PMADDWD Qd Pq Multiply and Add Packed Integers
									case 0xf6://PSADBW Qq Pq Compute Sum of Absolute Differences
									case 0xf7://MASKMOVQ Nq (DS:)[rDI] Store Selected Bytes of Quadword
									case 0xf8://PSUBB Qq Pq Subtract Packed Integers
									case 0xf9://PSUBW Qq Pq Subtract Packed Integers
									case 0xfa://PSUBD Qq Pq Subtract Packed Integers
									case 0xfb://PSUBQ Qq Pq Subtract Packed Quadword Integers
									case 0xfc://PADDB Qq Pq Add Packed Integers
									case 0xfd://PADDW Qq Pq Add Packed Integers
									case 0xfe://PADDD Qq Pq Add Packed Integers
									case 0xff:
										//default:
										abort(6);
										break;
									default:
										throw new NotImplementedException(string.Format("OPbyte 0x0f 0x{0:X} not implemented", OPbyte));
									case 0x06: //CLTS  CR0 Clear Task-Switched Flag in CR0
										if (cpu.cpl != 0)
											abort(13);
										set_CR0((uint)(cpu.cr0 & ~(1 << 3))); //Clear Task-Switched Flag in CR0
										goto EXEC_LOOP_END;
									case 0x20: //MOV Cd Rd Move to/from Control Registers
										if (cpu.cpl != 0)
											abort(13);
										mem8 = phys_mem8[physmem8_ptr++];
										if (!isRegisterAddressingMode)
											abort(6);
										reg_idx1 = regIdx1(mem8);
										switch (reg_idx1)
										{
											case 0:
												x = (uint)cpu.cr0;
												break;
											case 2:
												x = (uint)cpu.cr2;
												break;
											case 3:
												x = (uint)cpu.cr3;
												break;
											case 4:
												x = (uint)cpu.cr4;
												break;
											default:
												abort(6);
												break;
										}
										regs[regIdx0(mem8)] = x;
										goto EXEC_LOOP_END;
									case 0x22: //MOV Rd Cd Move to/from Control Registers
										if (cpu.cpl != 0)
											abort(13);
										mem8 = phys_mem8[physmem8_ptr++];
										if (!isRegisterAddressingMode)
											abort(6);
										reg_idx1 = regIdx1(mem8);
										x = regs[regIdx0(mem8)];
										switch (reg_idx1)
										{
											case 0:
												set_CR0(x);
												break;
											case 2:
												cpu.cr2 = (int)x;
												break;
											case 3:
												set_CR3((int)x);
												break;
											case 4:
												set_CR4((int)x);
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;
									case 0x23: //MOV Rd Dd Move to/from Debug Registers
										if (cpu.cpl != 0)
											abort(13);
										mem8 = phys_mem8[physmem8_ptr++];
										if (!isRegisterAddressingMode)
											abort(6);
										reg_idx1 = regIdx1(mem8);
										x = regs[regIdx0(mem8)];
										if (reg_idx1 == 4 || reg_idx1 == 5)
											abort(6);
										goto EXEC_LOOP_END;
									case 0x31: //RDTSC IA32_TIME_STAMP_COUNTER EAX Read Time-Stamp Counter
										if ((cpu.cr4 & (1 << 2)) != 0 && cpu.cpl != 0)
											abort(13);
										x = current_cycle_count();
										regs[0] = x >> 0;
										regs[2] = (uint)((x / 0x100000000) >> 0);
										goto EXEC_LOOP_END;
									case 0x80: //JO Jvds  Jump short if overflow (OF=1)
									case 0x81: //JNO Jvds  Jump short if not overflow (OF=0)
									case 0x82: //JB Jvds  Jump short if below/not above or equal/carry (CF=1)
									case 0x83: //JNB Jvds  Jump short if not below/above or equal/not carry (CF=0)
									case 0x84: //JZ Jvds  Jump short if zero/equal (ZF=0)
									case 0x85: //JNZ Jvds  Jump short if not zero/not equal (ZF=1)
									case 0x86: //JBE Jvds  Jump short if below or equal/not above (CF=1 AND ZF=1)
									case 0x87: //JNBE Jvds  Jump short if not below or equal/above (CF=0 AND ZF=0)
									case 0x88: //JS Jvds  Jump short if sign (SF=1)
									case 0x89: //JNS Jvds  Jump short if not sign (SF=0)
									case 0x8a: //JP Jvds  Jump short if parity/parity even (PF=1)
									case 0x8b: //JNP Jvds  Jump short if not parity/parity odd
									case 0x8c: //JL Jvds  Jump short if less/not greater (SF!=OF)
									case 0x8d: //JNL Jvds  Jump short if not less/greater or equal (SF=OF)
									case 0x8e: //JLE Jvds  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
									case 0x8f: //JNLE Jvds  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
										x = phys_mem8_uint();
										if (check_status_bits_for_jump(OPbyte & 0xf))
											physmem8_ptr = (physmem8_ptr + x) >> 0;
										goto EXEC_LOOP_END;
									case 0x90: //SETO  Eb Set Byte on Condition - overflow (OF=1)
									case 0x91: //SETNO  Eb Set Byte on Condition - not overflow (OF=0)
									case 0x92: //SETB  Eb Set Byte on Condition - below/not above or equal/carry (CF=1)
									case 0x93: //SETNB  Eb Set Byte on Condition - not below/above or equal/not carry (CF=0)
									case 0x94: //SETZ  Eb Set Byte on Condition - zero/equal (ZF=0)
									case 0x95: //SETNZ  Eb Set Byte on Condition - not zero/not equal (ZF=1)
									case 0x96: //SETBE  Eb Set Byte on Condition - below or equal/not above (CF=1 AND ZF=1)
									case 0x97: //SETNBE  Eb Set Byte on Condition - not below or equal/above (CF=0 AND ZF=0)
									case 0x98: //SETS  Eb Set Byte on Condition - sign (SF=1)
									case 0x99: //SETNS  Eb Set Byte on Condition - not sign (SF=0)
									case 0x9a: //SETP  Eb Set Byte on Condition - parity/parity even (PF=1)
									case 0x9b: //SETNP  Eb Set Byte on Condition - not parity/parity odd
									case 0x9c: //SETL  Eb Set Byte on Condition - less/not greater (SF!=OF)
									case 0x9d: //SETNL  Eb Set Byte on Condition - not less/greater or equal (SF=OF)
									case 0x9e: //SETLE  Eb Set Byte on Condition - less or equal/not greater ((ZF=1) OR (SF!=OF))
									case 0x9f: //SETNLE  Eb Set Byte on Condition - not less nor equal/greater ((ZF=0) AND (SF=OF))
										mem8 = phys_mem8[physmem8_ptr++];
										x = (uint)(check_status_bits_for_jump(OPbyte & 0xf) ? 1 : 0);
										if (isRegisterAddressingMode)
										{
											set_word_in_register(regIdx0(mem8), x);
										}
										else
										{
											segment_translation();
											st8_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xa0://PUSH FS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
									case 0xa8://PUSH GS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
										push_dword_to_stack((uint)cpu.segs[regIdx1(OPbyte)].selector);
										goto EXEC_LOOP_END;
									case 0xa1://POP SS:[rSP] FS Pop a Value from the Stack
									case 0xa9: //POP SS:[rSP] GS Pop a Value from the Stack
										set_segment_register((int)regIdx1(OPbyte), (int)(pop_dword_from_stack_read() & 0xffff));
										pop_dword_from_stack_incr_ptr();
										goto EXEC_LOOP_END;
									case 0xa2: //CPUID  IA32_BIOS_SIGN_ID CPU Identification
										op_CPUID();
										goto EXEC_LOOP_END;
									case 0xa3: //BT Evqp  Bit Test
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											mem8_loc = (mem8_loc + ((y >> 5) << 2)) >> 0;
											x = ld_32bits_mem8_read();
										}
										op_BT((int)x, (int)y);
										goto EXEC_LOOP_END;
									case 0xa4: //SHLD Gvqp Evqp Double Precision Shift Left
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										if (isRegisterAddressingMode)
										{
											z = phys_mem8[physmem8_ptr++];
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = op_SHLD(regs[reg_idx0], y, z);
										}
										else
										{
											segment_translation();
											z = phys_mem8[physmem8_ptr++];
											x = ld_32bits_mem8_write();
											x = op_SHLD(x, y, z);
											st32_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xa5: //SHLD Gvqp Evqp Double Precision Shift Left
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										z = (int)regs[1];
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = op_SHLD(regs[reg_idx0], y, z);
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											x = op_SHLD(x, y, z);
											st32_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xab: //BTS Gvqp Evqp Bit Test and Set
									case 0xb3: //BTR Gvqp Evqp Bit Test and Reset
									case 0xbb: //BTC Gvqp Evqp Bit Test and Complement
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										conditional_var = (int)((OPbyte >> 3) & 3);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = op_BTS_BTR_BTC(conditional_var, (int)regs[reg_idx0], (int)y);
										}
										else
										{
											segment_translation();
											mem8_loc = (mem8_loc + ((y >> 5) << 2)) >> 0;
											x = ld_32bits_mem8_write();
											x = op_BTS_BTR_BTC(conditional_var, (int)x, (int)y);
											st32_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xb1: //CMPXCHG Gvqp Evqp Compare and Exchange
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = regs[reg_idx0];
											y = do_32bit_math(5, regs[0], x);
											if (y == 0)
											{
												regs[reg_idx0] = regs[reg_idx1];
											}
											else
											{
												regs[0] = x;
											}
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											y = do_32bit_math(5, regs[0], x);
											if (y == 0)
											{
												st32_mem8_write(regs[reg_idx1]);
											}
											else
											{
												regs[0] = x;
											}
										}
										goto EXEC_LOOP_END;
									case 0xb7: //MOVZX Ew Gvqp Move with Zero-Extend
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)] & 0xffff;
										}
										else
										{
											segment_translation();
											x = ld_16bits_mem8_read();
										}
										regs[reg_idx1] = x;
										goto EXEC_LOOP_END;
									case 0xba: //BT Evqp  Bit Test
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										switch (conditional_var)
										{
											case 4: //BT Evqp  Bit Test
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
													y = phys_mem8[physmem8_ptr++];
												}
												else
												{
													segment_translation();
													y = phys_mem8[physmem8_ptr++];
													x = ld_32bits_mem8_read();
												}
												op_BT((int)x, (int)y);
												break;
											case 5: //BTS  Bit Test and Set
											case 6: //BTR  Bit Test and Reset
											case 7: //BTC  Bit Test and Complement
												if (isRegisterAddressingMode)
												{
													reg_idx0 = regIdx0(mem8);
													y = phys_mem8[physmem8_ptr++];
													regs[reg_idx0] = op_BTS_BTR_BTC(conditional_var & 3, (int)regs[reg_idx0], (int)y);
												}
												else
												{
													segment_translation();
													y = phys_mem8[physmem8_ptr++];
													x = ld_32bits_mem8_write();
													x = op_BTS_BTR_BTC(conditional_var & 3, (int)x, (int)y);
													st32_mem8_write(x);
												}
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;
									case 0xaf: //IMUL Evqp Gvqp Signed Multiply
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											y = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											y = ld_32bits_mem8_read();
										}
										regs[reg_idx1] = op_IMUL32((int)regs[reg_idx1], (int)y);
										goto EXEC_LOOP_END;
									case 0xbc: //BSF Evqp Gvqp Bit Scan Forward
									case 0xbd: //BSR Evqp Gvqp Bit Scan Reverse
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											y = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											y = ld_32bits_mem8_read();
										}
										if ((OPbyte & 1) != 0)
											regs[reg_idx1] = (uint)op_BSR((int)regs[reg_idx1], (int)y);
										else
											regs[reg_idx1] = (uint)op_BSF((int)regs[reg_idx1], (int)y);
										goto EXEC_LOOP_END;
									case 0xac: //SHRD Gvqp Evqp Double Precision Shift Right
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										if (isRegisterAddressingMode)
										{
											z = phys_mem8[physmem8_ptr++];
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = op_SHRD((int)regs[reg_idx0], y, z);
										}
										else
										{
											segment_translation();
											z = phys_mem8[physmem8_ptr++];
											x = ld_32bits_mem8_write();
											x = op_SHRD((int)x, y, z);
											st32_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xad: //SHRD Gvqp Evqp Double Precision Shift Right
										mem8 = phys_mem8[physmem8_ptr++];
										y = regs[regIdx1(mem8)];
										z = (int)regs[1];
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											regs[reg_idx0] = op_SHRD((int)regs[reg_idx0], y, z);
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											x = op_SHRD((int)x, y, z);
											st32_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0xb2: //LSS Mptp SS Load Far Pointer
									case 0xb4: //LFS Mptp FS Load Far Pointer
									case 0xb5: //LGS Mptp GS Load Far Pointer
										op_16_load_far_pointer32(OPbyteRegIdx0);
										goto EXEC_LOOP_END;
									case 0xb6: //MOVZX Eb Gvqp Move with Zero-Extend
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)) & 0xff;
										}
										else
										{
											segment_translation();
											x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
												? __ld_8bits_mem8_read()
												: phys_mem8[mem8_loc ^ last_tlb_val]);
										}
										regs[reg_idx1] = x;
										goto EXEC_LOOP_END;
									case 0xbe: //MOVSX Eb Gvqp Move with Sign-Extension
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
										}
										else
										{
											segment_translation();
											x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
												? __ld_8bits_mem8_read()
												: phys_mem8[mem8_loc ^ last_tlb_val]);
										}
										regs[reg_idx1] = (((x) << 24) >> 24);
										goto EXEC_LOOP_END;
									case 0xbf: //MOVSX Ew Gvqp Move with Sign-Extension
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_16bits_mem8_read();
										}
										regs[reg_idx1] = (((x) << 16) >> 16);
										goto EXEC_LOOP_END;
									case 0xc1: //XADD  Evqp Exchange and Add
										mem8 = phys_mem8[physmem8_ptr++];
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											x = regs[reg_idx0];
											y = do_32bit_math(0, x, regs[reg_idx1]);
											regs[reg_idx1] = x;
											regs[reg_idx0] = y;
										}
										else
										{
											segment_translation();
											x = ld_32bits_mem8_write();
											y = do_32bit_math(0, x, regs[reg_idx1]);
											st32_mem8_write(y);
											regs[reg_idx1] = x;
										}
										goto EXEC_LOOP_END;
									case 0xc8: //BSWAP  Zvqp Byte Swap
									case 0xc9:
									case 0xca:
									case 0xcb:
									case 0xcc:
									case 0xcd:
									case 0xce:
									case 0xcf:
										reg_idx1 = (int)(OPbyteRegIdx0);
										x = regs[reg_idx1];
										x = (x >> 24) | ((x >> 8) & 0x0000ff00) | ((x << 8) & 0x00ff0000) | (x << 24);
										regs[reg_idx1] = x;
										goto EXEC_LOOP_END;
								}
								break;
							/*
							 *  16bit Compatibility Mode Operator Routines
							    ==========================================================================================
							    0x1XX  corresponds to the 16-bit compat operator corresponding to the usual 0xXX
							 */
							default:
								switch (OPbyte)
								{
									case 0x101: //ADD Gvqp Evqp Add
									case 0x109: //OR Gvqp Evqp Logical Inclusive OR
									case 0x111: //ADC Gvqp Evqp Add with Carry
									case 0x119: //SBB Gvqp Evqp Integer Subtraction with Borrow
									case 0x121: //AND Gvqp Evqp Logical AND
									case 0x129: //SUB Gvqp Evqp Subtract
									case 0x131: //XOR Gvqp Evqp Logical Exclusive OR
									case 0x139: //CMP Evqp  Compare Two Operands
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = (int)(regIdx1(OPbyte));
										y = regs[regIdx1(mem8)];
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											set_lower_word_in_register(reg_idx0, do_16bit_math(conditional_var, regs[reg_idx0], y));
										}
										else
										{
											segment_translation();
											if (conditional_var != 7)
											{
												x = (uint)ld_16bits_mem8_write();
												x = do_16bit_math(conditional_var, x, y);
												st16_mem8_write(x);
											}
											else
											{
												x = ld_16bits_mem8_read();
												do_16bit_math(7, x, y);
											}
										}
										goto EXEC_LOOP_END;
									case 0x103: //ADD Evqp Gvqp Add
									case 0x10b: //OR Evqp Gvqp Logical Inclusive OR
									case 0x113: //ADC Evqp Gvqp Add with Carry
									case 0x11b: //SBB Evqp Gvqp Integer Subtraction with Borrow
									case 0x123: //AND Evqp Gvqp Logical AND
									case 0x12b: //SUB Evqp Gvqp Subtract
									case 0x133: //XOR Evqp Gvqp Logical Exclusive OR
									case 0x13b: //CMP Gvqp  Compare Two Operands
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = (int)(regIdx1(OPbyte));
										reg_idx1 = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											y = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											y = ld_16bits_mem8_read();
										}
										set_lower_word_in_register(reg_idx1, do_16bit_math(conditional_var, regs[reg_idx1], y));
										goto EXEC_LOOP_END;

									case 0x105: //ADD Ivds rAX Add
									case 0x10d: //OR Ivds rAX Logical Inclusive OR
									case 0x115: //ADC Ivds rAX Add with Carry
									case 0x11d: //SBB Ivds rAX Integer Subtraction with Borrow
									case 0x125: //AND Ivds rAX Logical AND
									case 0x12d: //SUB Ivds rAX Subtract
									case 0x135: //XOR Ivds rAX Logical Exclusive OR
									case 0x13d: //CMP rAX  Compare Two Operands
										y = (uint)ld16_mem8_direct();
										conditional_var = (int)(regIdx1(OPbyte));
										set_lower_word_in_register(0, do_16bit_math(conditional_var, regs[0], y));
										goto EXEC_LOOP_END;
									case 0x148: //DEC  Zv Decrement by 1
									case 0x149: //REX.WB   REX.W and REX.B combination
									case 0x14a: //REX.WX   REX.W and REX.X combination
									case 0x14b: //REX.WXB   REX.W, REX.X and REX.B combination
									case 0x14c: //REX.WR   REX.W and REX.R combination
									case 0x14d: //REX.WRB   REX.W, REX.R and REX.B combination
									case 0x14e: //REX.WRX   REX.W, REX.R and REX.X combination
									case 0x14f: //REX.WRXB   REX.W, REX.R, REX.X and REX.B combination
										reg_idx1 = (int)(OPbyteRegIdx0);
										set_lower_word_in_register(reg_idx1, decrement_16bit(regs[reg_idx1]));
										goto EXEC_LOOP_END;
									case 0x185: //TEST Evqp  Logical Compare
										mem8 = phys_mem8[physmem8_ptr++];
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_16bits_mem8_read();
										}
										y = regs[regIdx1(mem8)];
										{
											_dst = ((int)(x & y) << 16) >> 16;
											_op = 13;
										}
										goto EXEC_LOOP_END;
									case 0x189: //MOV Gvqp Evqp Move
										mem8 = phys_mem8[physmem8_ptr++];
										x = regs[regIdx1(mem8)];
										if (isRegisterAddressingMode)
										{
											set_lower_word_in_register(regIdx0(mem8), x);
										}
										else
										{
											segment_translation();
											st16_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0x190: //XCHG  Zvqp Exchange Register/Memory with Register
										goto EXEC_LOOP_END;
									case 0x1a1: //MOV Ovqp rAX Move
										mem8_loc = segmented_mem8_loc_for_MOV();
										x = ld_16bits_mem8_read();
										set_lower_word_in_register(0, x);
										goto EXEC_LOOP_END;
									case 0x1a3: //MOV rAX Ovqp Move
										mem8_loc = segmented_mem8_loc_for_MOV();
										st16_mem8_write(regs[0]);
										goto EXEC_LOOP_END;
									case 0x1ab: //STOS AX ES:[DI] Store String
										op_16_STOS();
										goto EXEC_LOOP_END;
									case 0x1b8: //MOV Ivqp Zvqp Move
									case 0x1b9:
									case 0x1ba:
									case 0x1bb:
									case 0x1bc:
									case 0x1bd:
									case 0x1be:
									case 0x1bf:
										set_lower_word_in_register((int)(OPbyteRegIdx0), (uint)ld16_mem8_direct());
										goto EXEC_LOOP_END;
									case 0x1c1: //ROL Ib Evqp Rotate
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											y = phys_mem8[physmem8_ptr++];
											reg_idx0 = regIdx0(mem8);
											set_lower_word_in_register(reg_idx0, shift16(conditional_var, regs[reg_idx0], (int)y));
										}
										else
										{
											segment_translation();
											y = phys_mem8[physmem8_ptr++];
											x = (uint)ld_16bits_mem8_write();
											x = shift16(conditional_var, x, (int)y);
											st16_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0x1c7: //MOV Ivds Evqp Move
										mem8 = phys_mem8[physmem8_ptr++];
										if (isRegisterAddressingMode)
										{
											x = (uint)ld16_mem8_direct();
											set_lower_word_in_register(regIdx0(mem8), x);
										}
										else
										{
											segment_translation();
											x = (uint)ld16_mem8_direct();
											st16_mem8_write(x);
										}
										goto EXEC_LOOP_END;
									case 0x181: //ADD Ivds Evqp Add
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											y = (uint)ld16_mem8_direct();
											regs[reg_idx0] = do_16bit_math(conditional_var, regs[reg_idx0], y);
										}
										else
										{
											segment_translation();
											y = (uint)ld16_mem8_direct();
											if (conditional_var != 7)
											{
												x = (uint)ld_16bits_mem8_write();
												x = do_16bit_math(conditional_var, x, y);
												st16_mem8_write(x);
											}
											else
											{
												x = ld_16bits_mem8_read();
												do_16bit_math(7, x, y);
											}
										}
										goto EXEC_LOOP_END;
									case 0x183: //ADD Ibs Evqp Add
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										if (isRegisterAddressingMode)
										{
											reg_idx0 = regIdx0(mem8);
											y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
											set_lower_word_in_register(reg_idx0, do_16bit_math(conditional_var, regs[reg_idx0], y));
										}
										else
										{
											segment_translation();
											y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
											if (conditional_var != 7)
											{
												x = (uint)ld_16bits_mem8_write();
												x = do_16bit_math(conditional_var, x, y);
												st16_mem8_write(x);
											}
											else
											{
												x = ld_16bits_mem8_read();
												do_16bit_math(7, x, y);
											}
										}
										goto EXEC_LOOP_END;
									case 0x18b: //MOV Evqp Gvqp Move
										mem8 = phys_mem8[physmem8_ptr++];
										if (isRegisterAddressingMode)
										{
											x = regs[regIdx0(mem8)];
										}
										else
										{
											segment_translation();
											x = ld_16bits_mem8_read();
										}
										set_lower_word_in_register(regIdx1(mem8), x);
										goto EXEC_LOOP_END;
									case 0x1a5://MOVS DS:[SI] ES:[DI] Move Data from String to String
										op_16_MOVS();
										goto EXEC_LOOP_END;
									case 0x1f7: //TEST Evqp  Logical Compare
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										switch (conditional_var)
										{
											case 0:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												y = (uint)ld16_mem8_direct();
												{
													_dst = (int)(((x & y) << 16) >> 16);
													_op = 13;
												}
												break;
											case 2:
												if (isRegisterAddressingMode)
												{
													reg_idx0 = regIdx0(mem8);
													set_lower_word_in_register(reg_idx0, ~regs[reg_idx0]);
												}
												else
												{
													segment_translation();
													x = (uint)ld_16bits_mem8_write();
													x = ~x;
													st16_mem8_write(x);
												}
												break;
											case 3:
												if (isRegisterAddressingMode)
												{
													reg_idx0 = regIdx0(mem8);
													set_lower_word_in_register(reg_idx0, do_16bit_math(5, 0, regs[reg_idx0]));
												}
												else
												{
													segment_translation();
													x = (uint)ld_16bits_mem8_write();
													x = do_16bit_math(5, 0, x);
													st16_mem8_write(x);
												}
												break;
											case 4:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												x = op_16_MUL(regs[0], x);
												set_lower_word_in_register(0, x);
												set_lower_word_in_register(2, x >> 16);
												break;
											case 5:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												x = op_16_IMUL(regs[0], x);
												set_lower_word_in_register(0, x);
												set_lower_word_in_register(2, x >> 16);
												break;
											case 6:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												op_16_DIV(x);
												break;
											case 7:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												op_16_IDIV(x);
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;
									case 0x1ff: //INC  Evqp Increment by 1
										mem8 = phys_mem8[physmem8_ptr++];
										conditional_var = regIdx1(mem8);
										switch (conditional_var)
										{
											case 0:
												if (isRegisterAddressingMode)
												{
													reg_idx0 = regIdx0(mem8);
													set_lower_word_in_register(reg_idx0, (uint)increment_16bit((int)regs[reg_idx0]));
												}
												else
												{
													segment_translation();
													x = (uint)ld_16bits_mem8_write();
													x = (uint)increment_16bit((int)x);
													st16_mem8_write(x);
												}
												break;
											case 1:
												if (isRegisterAddressingMode)
												{
													reg_idx0 = regIdx0(mem8);
													set_lower_word_in_register(reg_idx0, decrement_16bit(regs[reg_idx0]));
												}
												else
												{
													segment_translation();
													x = (uint)ld_16bits_mem8_write();
													x = decrement_16bit(x);
													st16_mem8_write(x);
												}
												break;
											case 2:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)] & 0xffff;
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												push_word_to_stack((eip + physmem8_ptr - initial_mem_ptr));
												eip = x;
												physmem8_ptr = initial_mem_ptr = 0;
												break;
											case 4:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)] & 0xffff;
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												eip = x;
												physmem8_ptr = initial_mem_ptr = 0;
												break;
											case 6:
												if (isRegisterAddressingMode)
												{
													x = regs[regIdx0(mem8)];
												}
												else
												{
													segment_translation();
													x = ld_16bits_mem8_read();
												}
												push_word_to_stack(x);
												break;
											case 3:
											case 5:
												if (isRegisterAddressingMode)
													abort(6);
												segment_translation();
												x = ld_16bits_mem8_read();
												mem8_loc = (mem8_loc + 2) >> 0;
												y = ld_16bits_mem8_read();
												if (conditional_var == 3)
													op_CALLF(0, y, x, (eip + physmem8_ptr - initial_mem_ptr));
												else
													op_JMPF(y, x);
												break;
											default:
												abort(6);
												break;
										}
										goto EXEC_LOOP_END;

									default:
										throw new NotImplementedException(string.Format("OPbyte 0x{0:X} not implemented", OPbyte));
								}
						}
					}

				EXEC_LOOP_END:
					{
					}
				} while (--cycles_left != 0); //End Giant Core DO WHILE Execution Loop
			OUTER_LOOP_END:
				{
				}

				#endregion

				cpu.cycle_count += (N_cycles - cycles_left);
				cpu.eip = eip + physmem8_ptr - initial_mem_ptr;
				cpu.cc_src = (int)_src;
				cpu.cc_dst = (int)_dst;
				cpu.cc_op = _op;
				cpu.cc_op2 = _op2;
				cpu.cc_dst2 = _dst2;
				return exit_code;
			}

			internal uint OPbyteRegIdx0
			{
				get { return regIdx0(OPbyte); }
			}

			#region Helpers

			public const int MOD_REGISTER_ADDRESSING_MODE = 3;

			/// <summary>
			/// MOD-REG-R/M byte
			/// MOD field specifies x86 addressing mode
			/// </summary>
			private int MOD
			{
				get { return (mem8 >> 6); }
			}

			internal bool isRegisterAddressingMode
			{
				get { return MOD == MOD_REGISTER_ADDRESSING_MODE; }
			}

			/// <summary>
			/// MOD-REG-R/M byte
			/// REG field specifies source or destination register
			/// </summary>
			internal static int regIdx1(int mem8)
			{
				return (mem8 >> 3) & 7;
			}

			internal static uint regIdx1(uint mem8)
			{
				return (mem8 >> 3) & 7;
			}

			/// <summary>
			/// MOD-REG-R/M byte
			/// R/M field combiden with mode, specifies either
			/// 1. the second operand in two-operand instruction, or
			/// 2. the only operand in a single-operand instruction
			/// </summary>
			internal static int regIdx0(int mem8)
			{
				return mem8 & 7;
			}

			private static uint regIdx0(uint mem8)
			{
				return mem8 & 7;
			}

			internal uint phys_mem8_uint()
			{
				return (uint) (phys_mem8[physmem8_ptr++] | (phys_mem8[physmem8_ptr++] << 8) |
				               (phys_mem8[physmem8_ptr++] << 16) | (phys_mem8[physmem8_ptr++] << 24));
			}

			private int decrement_8bit(uint p0)
			{
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = _dst;
				}
				_dst = ((int)(x - 1) << 24) >> 24;
				_op = 28;
				return _dst;
			}

			private int increment_8bit(uint p0)
			{
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = _dst;
				}
				_dst = ((int)(x + 1) << 24) >> 24;
				_op = 25;
				return _dst;
			}

			private void op_POPA()
			{
				int reg_idx1;
				mem8_loc = (uint)(((regs[4] & SS_mask) + SS_base) >> 0);
				for (reg_idx1 = 7; reg_idx1 >= 0; reg_idx1--)
				{
					if (reg_idx1 != 4)
					{
						regs[reg_idx1] = ld_32bits_mem8_read();
					}
					mem8_loc = (mem8_loc + 4) >> 0;
				}
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((regs[4] + 32) & SS_mask));
			}

			private void op_PUSHA()
			{
				int reg_idx1;
				var y = (regs[4] - 32) >> 0;
				mem8_loc = (uint)(((y & SS_mask) + SS_base) >> 0);
				for (reg_idx1 = 7; reg_idx1 >= 0; reg_idx1--)
				{
					var x = regs[reg_idx1];
					st32_mem8_write(x);
					mem8_loc = (mem8_loc + 4) >> 0;
				}
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((y) & SS_mask));
			}

			private void op_16_MOVS()
			{
				int Xf;
				int eg;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				var Yf = regs[7];
				mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				eg = (int)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					{
						x = ld_16bits_mem8_read();
						mem8_loc = (uint)eg;
						st16_mem8_write(x);
						regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 1)) & Xf));
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 1)) & Xf));
						regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
						if ((ag & Xf) != 0)
							physmem8_ptr = initial_mem_ptr;
					}
				}
				else
				{
					x = ld_16bits_mem8_read();
					mem8_loc = (uint)eg;
					st16_mem8_write(x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 1)) & Xf));
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 1)) & Xf));
				}
			}

			private void op_16_STOS()
			{
				int Xf;
				int ag;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					ag = (int)regs[1];
					if ((ag & Xf) == 0)
						return;
					{
						st16_mem8_write(regs[0]);
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 1)) & Xf));
						regs[1] = (uint)(ag = (ag & ~Xf) | ((ag - 1) & Xf));
						if ((ag & Xf) != 0)
							physmem8_ptr = initial_mem_ptr;
					}
				}
				else
				{
					st16_mem8_write(regs[0]);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 1)) & Xf));
				}
			}

			private uint decrement_16bit(uint x)
			{
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = _dst;
				}
				_dst = (((int)(x - 1) << 16) >> 16);
				_op = 29;
				return u_dst;
			}

			private int increment_16bit(int x)
			{
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = _dst;
				}
				_dst = (((x + 1) << 16) >> 16);
				_op = 26;
				return _dst;
			}

			private uint shift16(int conditional_var, uint Yb, int Zb)
			{
				int kc;
				int ac;
				switch (conditional_var)
				{
					case 0:
						if ((Zb & 0x1f) != 0)
						{
							Zb &= 0xf;
							Yb &= 0xffff;
							kc = (int)Yb;
							Yb = (Yb << Zb) | (Yb >> (16 - Zb));
							_src = (int)conditional_flags_for_rot_shift_ops();
							_src = (int)(_src | ((Yb & 0x0001) | (((kc ^ Yb) >> 4) & 0x0800)));
							_dst = ((_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 1:
						if ((Zb & 0x1f) != 0)
						{
							Zb &= 0xf;
							Yb &= 0xffff;
							kc = (int)Yb;
							Yb = (Yb >> Zb) | (Yb << (16 - Zb));
							_src = (int)conditional_flags_for_rot_shift_ops();
							_src = (int)(_src | (((Yb >> 15) & 0x0001) | (((kc ^ Yb) >> 4) & 0x0800)));
							_dst = ((_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 2:
						Zb = cpu.shift16_LUT[Zb & 0x1f];
						if (Zb != 0)
						{
							Yb &= 0xffff;
							kc = (int)Yb;
							ac = (int)check_carry();
							Yb = (uint)((Yb << Zb) | (ac << (Zb - 1)));
							if (Zb > 1)
								Yb = (uint)(Yb | (kc >> (17 - Zb)));
							_src = (int)conditional_flags_for_rot_shift_ops();
							_src = (int)(_src | ((((kc ^ Yb) >> 4) & 0x0800) | ((kc >> (16 - Zb)) & 0x0001)));
							_dst = ((_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 3:
						Zb = cpu.shift16_LUT[Zb & 0x1f];
						if (Zb != 0)
						{
							Yb &= 0xffff;
							kc = (int)Yb;
							ac = (int)check_carry();
							Yb = (uint)((Yb >> Zb) | (ac << (16 - Zb)));
							if (Zb > 1)
								Yb = (uint)(Yb | (kc << (17 - Zb)));
							_src = (int)conditional_flags_for_rot_shift_ops();
							_src = (int)(_src | ((((kc ^ Yb) >> 4) & 0x0800) | ((kc >> (Zb - 1)) & 0x0001)));
							_dst = ((_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 4:
					case 6:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							_src = (int)(Yb << (Zb - 1));
							_dst = (int)(Yb = (((Yb << Zb) << 16) >> 16));
							_op = 16;
						}
						break;
					case 5:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							Yb &= 0xffff;
							_src = (int)(Yb >> (Zb - 1));
							_dst = (int)(Yb = (((Yb >> Zb) << 16) >> 16));
							_op = 19;
						}
						break;
					case 7:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							Yb = (Yb << 16) >> 16;
							_src = (int)(Yb >> (Zb - 1));
							_dst = (int)(Yb = (((Yb >> Zb) << 16) >> 16));
							_op = 19;
						}
						break;
					default:
						throw new Exception("unsupported shift16=" + conditional_var);
				}
				return Yb;
			}

			private void op_LEAVE()
			{
				throw new NotImplementedException();
			}

			private void op_IRET(uint is_32_bit)
			{
				if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
				{
					if ((cpu.eflags & 0x00020000) != 0)
					{
						var iopl = (cpu.eflags >> 12) & 3;
						if (iopl != 3)
							abort(13);
					}
					do_return_not_protected_mode(is_32_bit, 1, 0);
				}
				else
				{
					if ((cpu.eflags & 0x00004000) != 0)
					{
						throw new Exception("unsupported task gate");
					}
					else
					{
						do_return_protected_mode(is_32_bit, 1, 0);
					}
				}
			}

			private void do_return_protected_mode(uint is_32_bit, int is_iret, int imm16)
			{
				int selector;
				int gf;
				int stack_eip;
				int wd;
				var SS_mask = SS_mask_from_flags(cpu.segs[2].flags);
				var esp = regs[4];
				var qe = cpu.segs[2].@base;
				var stack_eflags = 0;
				if (is_32_bit == 1)
				{
					{
						mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
						stack_eip = ld32_mem8_kernel_read();
						esp = (uint)((esp + 4) & -1);
					}
					{
						mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
						selector = ld32_mem8_kernel_read();                //CS selector
						esp = (uint)((esp + 4) & -1);
					}
					selector &= 0xffff;
					if (is_iret != 0)
					{
						{
							mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
							stack_eflags = ld32_mem8_kernel_read();
							esp = (uint)((esp + 4) & -1);
						}
						if ((stack_eflags & 0x00020000) != 0)
						{     //eflags.VM (return to v86 mode)
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								wd = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							//pop segment selectors from stack
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								gf = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							int hf;
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								hf = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							int jf;
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								jf = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							int kf;
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								kf = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							int lf;
							{
								mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
								lf = ld32_mem8_kernel_read();
								esp = (uint)((esp + 4) & -1);
							}
							set_FLAGS((uint)stack_eflags, 0x00000100 | 0x00040000 | 0x00200000 | 0x00000200 | 0x00003000 | 0x00020000 | 0x00004000 | 0x00080000 | 0x00100000);
							init_segment_vars_with_selector(1, selector & 0xffff);
							change_permission_level(3);
							init_segment_vars_with_selector(2, gf & 0xffff);
							init_segment_vars_with_selector(0, hf & 0xffff);
							init_segment_vars_with_selector(3, jf & 0xffff);
							init_segment_vars_with_selector(4, kf & 0xffff);
							init_segment_vars_with_selector(5, lf & 0xffff);
							eip = (uint)(stack_eip & 0xffff);
							physmem8_ptr = initial_mem_ptr = 0;
							regs[4] = (uint)((regs[4] & ~SS_mask) | ((wd) & SS_mask));
							return;
						}
					}
				}
				else
				{
					{
						mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
						stack_eip = ld16_mem8_kernel_read();
						esp = (uint)((esp + 2) & -1);
					}
					{
						mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
						selector = ld16_mem8_kernel_read();
						esp = (uint)((esp + 2) & -1);
					}
					if (is_iret != 0)
					{
						mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
						stack_eflags = ld16_mem8_kernel_read();
						esp = (uint)((esp + 2) & -1);
					}
				}
				if ((selector & 0xfffc) == 0)
					abort_with_error_code(13, selector & 0xfffc);
				var e = load_from_descriptor_table((uint)selector);
				if (e == null)
					abort_with_error_code(13, selector & 0xfffc);
				var descriptor_low4bytes = e[0];
				var descriptor_high4bytes = e[1];
				if ((descriptor_high4bytes & (1 << 12)) == 0 || (descriptor_high4bytes & (1 << 11)) == 0)
					abort_with_error_code(13, selector & 0xfffc);
				var cpl_var = cpu.cpl;
				var rpl = selector & 3;
				if (rpl < cpl_var)
					abort_with_error_code(13, selector & 0xfffc);
				var dpl = (descriptor_high4bytes >> 13) & 3;
				if ((descriptor_high4bytes & (1 << 10)) != 0)
				{
					if (dpl > rpl)
						abort_with_error_code(13, selector & 0xfffc);
				}
				else
				{
					if (dpl != rpl)
						abort_with_error_code(13, selector & 0xfffc);
				}
				if ((descriptor_high4bytes & (1 << 15)) == 0)
					abort_with_error_code(11, selector & 0xfffc);
				esp = (uint)((esp + imm16) & -1);
				if (rpl == cpl_var)
				{
					set_segment_vars(1, selector, (uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes), calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes), descriptor_high4bytes);
				}
				else
				{
					if (is_32_bit == 1)
					{
						{
							mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
							wd = ld32_mem8_kernel_read();
							esp = (uint)((esp + 4) & -1);
						}
						{
							mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
							gf = ld32_mem8_kernel_read();
							esp = (uint)((esp + 4) & -1);
						}
						gf &= 0xffff;
					}
					else
					{
						{
							mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
							wd = ld16_mem8_kernel_read();
							esp = (uint)((esp + 2) & -1);
						}
						{
							mem8_loc = (uint)((qe + (esp & SS_mask)) & -1);
							gf = ld16_mem8_kernel_read();
							esp = (uint)((esp + 2) & -1);
						}
					}
					int xe = 0;
					if ((gf & 0xfffc) == 0)
					{
						abort_with_error_code(13, 0);
					}
					else
					{
						if ((gf & 3) != rpl)
							abort_with_error_code(13, gf & 0xfffc);
						e = load_from_descriptor_table((uint)gf);
						if (e == null)
							abort_with_error_code(13, gf & 0xfffc);
						var we = e[0];
						xe = e[1];
						if ((xe & (1 << 12)) == 0 || (xe & (1 << 11)) != 0 || (xe & (1 << 9)) == 0)
							abort_with_error_code(13, gf & 0xfffc);
						dpl = (xe >> 13) & 3;
						if (dpl != rpl)
							abort_with_error_code(13, gf & 0xfffc);
						if ((xe & (1 << 15)) == 0)
							abort_with_error_code(11, gf & 0xfffc);
						set_segment_vars(2, gf, (uint)calculate_descriptor_base(we, xe), calculate_descriptor_limit(we, xe), xe);
					}
					set_segment_vars(1, selector, (uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes), calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes), descriptor_high4bytes);
					change_permission_level(rpl);
					esp = (uint)wd;
					SS_mask = SS_mask_from_flags(xe);
					Pe(0, rpl);
					Pe(3, rpl);
					Pe(4, rpl);
					Pe(5, rpl);
					esp = (uint)((esp + imm16) & -1);
				}
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((esp) & SS_mask));
				eip = (uint)stack_eip;
				physmem8_ptr = initial_mem_ptr = 0;
				if (is_iret != 0)
				{
					var ef = 0x00000100 | 0x00040000 | 0x00200000 | 0x00010000 | 0x00004000;
					if (cpl_var == 0)
						ef |= 0x00003000;
					int iopl = (cpu.eflags >> 12) & 3;
					if (cpl_var <= iopl)
						ef |= 0x00000200;
					if (is_32_bit == 0)
						ef &= 0xffff;
					set_FLAGS((uint)stack_eflags, ef);
				}
			}

			/* used only in do_return_protected_mode */
			private void Pe(int register, int cpl_var)
			{
				if ((register == 4 || register == 5) && (cpu.segs[register].selector & 0xfffc) == 0)
					return;
				var descriptor_high4bytes = cpu.segs[register].flags;
				var dpl = (descriptor_high4bytes >> 13) & 3;
				if ((descriptor_high4bytes & (1 << 11)) == 0 || (descriptor_high4bytes & (1 << 10)) == 0)
				{
					if (dpl < cpl_var)
					{
						set_segment_vars(register, 0, 0, 0, 0);
					}
				}
			}

			private int ld16_mem8_kernel_read()
			{
				int tlb_lookup;
				return (((tlb_lookup = cpu.tlb_read_kernel[mem8_loc >> 12]) | mem8_loc) & 1) != 0 ? __ld16_mem8_kernel_read() : phys_mem16[(mem8_loc ^ tlb_lookup) >> 1];
			}

			private int __ld16_mem8_kernel_read()
			{
				var x = ld8_mem8_kernel_read();
				mem8_loc++;
				x |= ld8_mem8_kernel_read() << 8;
				mem8_loc--;
				return x;
			}

			private void do_return_not_protected_mode(uint is32Bit, int i, int i1)
			{
				throw new NotImplementedException();
			}

			private void op_16_IDIV(uint u)
			{
				throw new NotImplementedException();
			}

			private void op_16_DIV(uint OPbyte)
			{
				var a = (regs[2] << 16) | (regs[0] & 0xffff);
				OPbyte &= 0xffff;
				if ((a >> 16) >= OPbyte)
					abort(0);
				var q = (a / OPbyte) >> 0;
				var r = (a % OPbyte);
				set_lower_word_in_register(0, q);
				set_lower_word_in_register(2, r);
			}

			private uint op_16_IMUL(uint u, uint u1)
			{
				throw new NotImplementedException();
			}

			private uint op_16_MUL(uint u, uint u1)
			{
				throw new NotImplementedException();
			}

			private uint op_SHLD(uint Yb, uint Zb, int pc)
			{
				pc &= 0x1f;
				if (pc != 0)
				{
					_src = (int)(Yb << (pc - 1));
					_dst = (int)(Yb = (Yb << pc) | (Zb >> (32 - pc)));
					_op = 17;
				}
				return Yb;
			}

			private void stringOp_STOSB()
			{
				int Xf;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					{
						st8_mem8_write(regs[0]);
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
						regs[1] = (uint)(ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf)));
						if ((ag & Xf) != 0)
							physmem8_ptr = initial_mem_ptr;
					}
				}
				else
				{
					st8_mem8_write(regs[0]);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				}
			}

			private uint current_cycle_count()
			{
				return cpu.cycle_count + (N_cycles - cycles_left);
			}

			private void stringOp_CMPSD()
			{
				throw new NotImplementedException();
			}

			private void stringOp_CMPSB()
			{
				int Xf;
				int eg;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				var Yf = regs[7];
				mem8_loc = (uint)((cg & Xf) + cpu.segs[Sb].@base) >> 0;
				eg = (int)((Yf & Xf) + cpu.segs[0].@base) >> 0;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					x = ld_8bits_mem8_read();
					mem8_loc = (uint)eg;
					y = ld_8bits_mem8_read();
					do_8bit_math(7, x, y);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
					regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((CS_flags & 0x0010) != 0)
					{
						if (_dst != 0)
							return;
					}
					else
					{
						if ((_dst == 0))
							return;
					}
					if ((ag & Xf) != 0)
						physmem8_ptr = initial_mem_ptr;
				}
				else
				{
					x = ld_8bits_mem8_read();
					mem8_loc = (uint)eg;
					y = ld_8bits_mem8_read();
					do_8bit_math(7, x, y);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				}
			}

			private void op_BT(int Yb, int Zb)
			{
				Zb &= 0x1f;
				_src = Yb >> Zb;
				_op = 20;
			}

			private int op_BSF(int Yb, int Zb)
			{
				if (Zb != 0)
				{
					Yb = 0;
					while ((Zb & 1) == 0)
					{
						Yb++;
						Zb >>= 1;
					}
					_dst = 1;
				}
				else
				{
					_dst = 0;
				}
				_op = 14;
				return Yb;
			}

			private int op_BSR(int Yb, int Zb)
			{
				if (Zb != 0)
				{
					Yb = 31;
					while (Zb >= 0)
					{
						Yb--;
						Zb <<= 1;
					}
					_dst = 1;
				}
				else
				{
					_dst = 0;
				}
				_op = 14;
				return Yb;
			}

			private void stringOp_SCASD()
			{
				int Xf;
				int x;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					x = (int)ld_32bits_mem8_read();
					do_32bit_math(7, regs[0], (uint)x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
					regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((CS_flags & 0x0010) != 0)
					{
						if (_dst != 0)
							return;
					}
					else
					{
						if ((_dst == 0))
							return;
					}
					if ((ag & Xf) != 0)
						physmem8_ptr = initial_mem_ptr;
				}
				else
				{
					x = (int)ld_32bits_mem8_read();
					do_32bit_math(7, regs[0], (uint)x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
				}
			}

			private uint op_SHRD(int Yb, uint Zb, int pc)
			{
				pc &= 0x1f;
				if (pc != 0)
				{
					_src = Yb >> (pc - 1);
					_dst = Yb = (int)(((uint)Yb >> pc) | (Zb << (32 - pc)));
					_op = 20;
				}
				return (uint)Yb;
			}

			private void stringOp_MOVSB()
			{
				int Xf;
				long eg;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				var Yf = regs[7];
				mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				eg = ((Yf & Xf) + cpu.segs[0].@base) >> 0;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					{
						x = ld_8bits_mem8_read();
						mem8_loc = (uint)eg;
						st8_mem8_write(x);
						regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
						regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
						if ((ag & Xf) != 0)
							physmem8_ptr = initial_mem_ptr;
					}
				}
				else
				{
					x = ld_8bits_mem8_read();
					mem8_loc = (uint)eg;
					st8_mem8_write(x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				}
			}

			private uint do_16bit_math(int conditional_var, uint Yb, uint Zb)
			{
				uint ac;
				switch (conditional_var)
				{
					case 0:
						u_src = Zb;
						Yb = (((Yb + Zb) << 16) >> 16);
						u_dst = Yb;
						_op = 1;
						break;
					case 1:
						Yb = (((Yb | Zb) << 16) >> 16);
						u_dst = Yb;
						_op = 13;
						break;
					case 2:
						ac = check_carry();
						u_src = Zb;
						Yb = (((Yb + Zb + ac) << 16) >> 16);
						u_dst = Yb;
						_op = ac != 0 ? 4 : 1;
						break;
					case 3:
						ac = check_carry();
						u_src = Zb;
						Yb = (((Yb - Zb - ac) << 16) >> 16);
						u_dst = Yb;
						_op = ac != 0 ? 10 : 7;
						break;
					case 4:
						Yb = (((Yb & Zb) << 16) >> 16);
						u_dst = Yb;
						_op = 13;
						break;
					case 5:
						u_src = Zb;
						Yb = (((Yb - Zb) << 16) >> 16);
						u_dst = Yb;
						_op = 7;
						break;
					case 6:
						Yb = (((Yb ^ Zb) << 16) >> 16);
						u_dst = Yb;
						_op = 13;
						break;
					case 7:
						u_src = Zb;
						_dst = (((int)(Yb - Zb) << 16) >> 16);
						_op = 7;
						break;
					default:
						throw new Exception("arith" + conditional_var + ": invalid op");
				}
				return Yb;
			}

			private void stringOp_SCASB()
			{
				int Xf;
				uint x;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					x = ld_8bits_mem8_read();
					do_8bit_math(7, regs[0], x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
					regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((CS_flags & 0x0010) != 0)
					{
						if (!(u_dst == 0))
							return;
					}
					else
					{
						if ((u_dst == 0))
							return;
					}
					if ((ag & Xf) != 0)
						physmem8_ptr = initial_mem_ptr;
				}
				else
				{
					x = ld_8bits_mem8_read();
					do_8bit_math(7, regs[0], x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				}
			}

			private void stringOp_LODSB()
			{
				int Xf;
				uint x;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[1];
					if ((ag & Xf) == 0)
						return;
					x = ld_8bits_mem8_read();
					regs[0] = (uint)((regs[0] & -256) | x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
					regs[1] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((ag & Xf) != 0)
						physmem8_ptr = initial_mem_ptr;
				}
				else
				{
					x = ld_8bits_mem8_read();
					regs[0] = (uint)((regs[0] & -256) | x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 0)) & Xf));
				}
			}

			private uint op_IDIV32(int Ic, uint Jc, int OPbyte)
			{
				int Mc;
				int Nc;
				if (Ic < 0)
				{
					Mc = 1;
					Ic = ~Ic;
					Jc = (uint)((-Jc) >> 0);
					if (Jc == 0)
						Ic = (Ic + 1) >> 0;
				}
				else
				{
					Mc = 0;
				}
				if (OPbyte < 0)
				{
					OPbyte = (-OPbyte) >> 0;
					Nc = 1;
				}
				else
				{
					Nc = 0;
				}
				var q = op_DIV32((uint)Ic, Jc, (uint)OPbyte);
				Nc ^= Mc;
				if (Nc != 0)
				{
					if ((q >> 0) > 0x80000000)
						abort(0);
					q = (uint)((-q) >> 0);
				}
				else
				{
					if ((q >> 0) >= 0x80000000)
						abort(0);
				}
				if (Mc != 0)
				{
					v = (uint)((-v) >> 0);
				}
				return q;
			}

			private uint op_DIV32(uint Ic, uint Jc, uint OPbyte)
			{
				Ic = Ic >> 0;
				Jc = Jc >> 0;
				OPbyte = OPbyte >> 0;
				if (Ic >= OPbyte)
				{
					abort(0);
				}
				if (Ic >= 0 && Ic <= 0x200000)
				{
					var a = Ic * 4294967296 + Jc;
					v = (uint)((a % OPbyte) >> 0);
					return (uint)((a / OPbyte) >> 0);
				}
				else
				{
					for (var i = 0; i < 32; i++)
					{
						var Kc = Ic >> 31;
						Ic = ((Ic << 1) | (Jc >> 31)) >> 0;
						if (Kc != 0 || Ic >= OPbyte)
						{
							Ic = Ic - OPbyte;
							Jc = (Jc << 1) | 1;
						}
						else
						{
							Jc = Jc << 1;
						}
					}
					v = Ic >> 0;
					return Jc;
				}
			}

			private uint op_IMUL32(int a, int OPbyte)
			{
				var s = 0;
				if (a < 0)
				{
					a = -a;
					s = 1;
				}
				if (OPbyte < 0)
				{
					OPbyte = -OPbyte;
					s ^= 1;
				}
				var r = do_multiply32((uint)a, (uint)OPbyte);
				if (s != 0)
				{
					v = ~v;
					r = (-r) >> 0;
					if (r == 0)
					{
						v = (v + 1) >> 0;
					}
				}
				_dst = r;
				_src = (int)((v - (r >> 31)) >> 0);
				_op = 23;
				return (uint)r;
			}

			private int op_MUL32(uint a, uint OPbyte)
			{
				_dst = do_multiply32(a, OPbyte);
				_src = (int)v;
				_op = 23;
				return _dst;
			}

			private int do_multiply32(uint a, uint OPbyte)
			{
				//throw new NotImplementedException();
				//todo: makes infinitive loop
				uint m;
				a = a >> 0;
				OPbyte = OPbyte >> 0;
				var r = (long)a * OPbyte;
				if (r <= 0xffffffff)
				{
					v = 0;
					r = (uint)(r & -1);
				}
				else
				{
					var Jc = a & 0xffff;
					var Ic = a >> 16;
					var Tc = OPbyte & 0xffff;
					var Uc = OPbyte >> 16;
					r = Jc * Tc;
					v = Ic * Uc;
					m = Jc * Uc;
					r += (((m & 0xffff) << 16) >> 0);
					v += (m >> 16);
					if (r >= 4294967296)
					{
						r = (uint)(r - 4294967296);
						v++;
					}
					m = Ic * Tc;
					r += (((m & 0xffff) << 16) >> 0);
					v += (m >> 16);
					if (r >= 4294967296)
					{
						r = (uint)(r - 4294967296);
						v++;
					}
					r = (uint)(r & -1);
					v = (uint)(v & -1);
				}
				return (int)r;
			}

			private void op_IDIV(uint u)
			{
				throw new NotImplementedException();
			}

			private void op_DIV(uint OPbyte)
			{
				var a = regs[0] & 0xffff;
				OPbyte &= 0xff;
				if ((a >> 8) >= OPbyte)
					abort(0);
				var q = (a / OPbyte) >> 0;
				var r = (a % OPbyte);
				set_lower_word_in_register(0, (q & 0xff) | (r << 8));
			}

			private uint op_IMUL(uint u, uint u1)
			{
				throw new NotImplementedException();
			}

			private uint op_MUL(uint u, uint u1)
			{
				throw new NotImplementedException();
			}

			private void op_CALLF(int i, uint u, uint u1, uint u2)
			{
				throw new NotImplementedException();
			}

			private bool check_status_bits_for_jump(uint gd)
			{
				bool result;
				switch (gd >> 1)
				{
					case 0:
						result = check_overflow() != 0;
						break;
					case 1:
						result = check_carry() != 0;
						break;
					case 2:
						result = (u_dst == 0);
						break;
					case 3:
						result = check_below_or_equal();
						break;
					case 4:
						result = (_op == 24 ? ((_src >> 7) & 1) != 0 : ((int)_dst < 0));
						break;
					case 5:
						result = check_parity() != 0;
						break;
					case 6:
						result = check_less_than();
						break;
					case 7:
						result = check_less_or_equal();
						break;
					default:
						throw new Exception("unsupported cond: " + gd);
				}
				return result ^ (gd & 1) != 0;
			}

			private void op_LAR_LSL(uint u, uint u1)
			{
				throw new NotImplementedException();
			}

			private uint op_BTS_BTR_BTC(int conditional_var, int Yb, int Zb)
			{
				Zb &= 0x1f;
				_src = (Yb >> Zb);
				var wc = 1 << Zb;
				switch (conditional_var)
				{
					case 1:
						Yb |= wc;
						break;
					case 2:
						Yb &= ~wc;
						break;
					case 3:
					default:
						Yb ^= wc;
						break;
				}
				_op = 20;
				return (uint)Yb;
			}

			private void op_VERR_VERW(uint u, int i)
			{
				throw new NotImplementedException();
			}

			private void op_LTR(int selector)
			{
				selector &= 0xffff;
				if ((selector & 0xfffc) == 0)
				{
					cpu.tr.@base = 0;
					cpu.tr.limit = 0;
					cpu.tr.flags = 0;
				}
				else
				{
					if ((selector & 0x4) != 0)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					var descriptor_table = cpu.gdt;
					var Rb = selector & ~7;
					var De = 7;
					if ((Rb + De) > descriptor_table.limit)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					mem8_loc = (uint)((descriptor_table.@base + Rb) & -1);
					var descriptor_low4bytes = ld32_mem8_kernel_read();
					mem8_loc += 4;
					var descriptor_high4bytes = ld32_mem8_kernel_read();
					var descriptor_type = (descriptor_high4bytes >> 8) & 0xf;
					if ((descriptor_high4bytes & (1 << 12)) != 0 || (descriptor_type != 1 && descriptor_type != 9))
						abort_with_error_code(13, (int)(selector & 0xfffc));
					if ((descriptor_high4bytes & (1 << 15)) == 0)
						abort_with_error_code(11, (int)(selector & 0xfffc));
					set_descriptor_register(cpu.tr, descriptor_low4bytes, descriptor_high4bytes);
					descriptor_high4bytes |= (1 << 9);
					st32_mem8_kernel_write(descriptor_high4bytes);
				}
				cpu.tr.selector = (int)selector;
			}

			private void op_LDTR(uint selector)
			{
				selector &= 0xffff;
				if ((selector & 0xfffc) == 0)
				{
					cpu.ldt.@base = 0;
					cpu.ldt.limit = 0;
				}
				else
				{
					if ((selector & 0x4) != 0)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					var descriptor_table = cpu.gdt;
					var Rb = selector & ~7;
					var De = 7;
					if ((Rb + De) > descriptor_table.limit)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					mem8_loc = (uint)((descriptor_table.@base + Rb) & -1);
					var descriptor_low4bytes = ld32_mem8_kernel_read();
					mem8_loc += 4;
					var descriptor_high4bytes = ld32_mem8_kernel_read();
					if ((descriptor_high4bytes & (1 << 12)) != 0 || ((descriptor_high4bytes >> 8) & 0xf) != 2)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					if ((descriptor_high4bytes & (1 << 15)) == 0)
						abort_with_error_code(11, (int)(selector & 0xfffc));
					set_descriptor_register(cpu.ldt, descriptor_low4bytes, descriptor_high4bytes);
				}
				cpu.ldt.selector = (int)selector;
			}

			/* Used to set TR and LDTR */

			private void set_descriptor_register(Segment descriptor_table, int descriptor_low4bytes, int descriptor_high4bytes)
			{
				descriptor_table.@base = (uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes);
				descriptor_table.limit = calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes);
				descriptor_table.flags = descriptor_high4bytes;
			}

			private void op_CPUID()
			{
				var eax = regs[REG_AX];
				switch (eax)
				{
					case 0: // eax == 0: vendor ID
						regs[REG_AX] = 1;
						regs[REG_BX] = 0x756e6547 & -1;
						regs[2] = 0x49656e69 & -1;
						regs[REG_CX] = 0x6c65746e & -1;
						break;
					case 1: // eax == 1: processor info and feature flags
					default:
						regs[REG_AX] = (5 << 8) | (4 << 4) | 3; // family | model | stepping
						regs[REG_BX] = 8 << 8;   // danluu: This is a mystery to me. This bit now indicates clflush line size, but must have meant something else in the past.
						regs[REG_CX] = 0;
						regs[2] = (1 << 4); // rdtsc support
						break;
				}
			}

			private void push_word_to_stack(uint u)
			{
				throw new NotImplementedException();
			}

			private uint get_FLAGS()
			{
				var flag_bits = get_conditional_flags();
				flag_bits |= (uint)(cpu.df & 0x00000400); //direction flag
				flag_bits |= (uint)cpu.eflags; //get extended flags
				return flag_bits;
			}

			private void set_FLAGS(uint flag_bits, long ld)
			{
				u_src = flag_bits & (0x0800 | 0x0080 | 0x0040 | 0x0010 | 0x0004 | 0x0001);
				u_dst = ((u_src >> 6) & 1) ^ 1;
				_op = 24;
				cpu.df = (int)(1 - (2 * ((flag_bits >> 10) & 1)));
				cpu.eflags = (int)((cpu.eflags & ~ld) | (flag_bits & ld));
			}

			private void pop_word_from_stack_incr_ptr()
			{
				throw new NotImplementedException();
			}

			private uint pop_word_from_stack_read()
			{
				throw new NotImplementedException();
			}

			private void op_16_load_far_pointer32(uint Sb)
			{
				var mem8 = phys_mem8[physmem8_ptr++];
				if ((mem8 >> 3) == 3)
					abort(6);
				mem8_loc = segment_translation(mem8);
				var x = ld_32bits_mem8_read();
				mem8_loc += 4;
				var y = ld_16bits_mem8_read();
				set_segment_register((int)Sb, (int)y);
				regs[regIdx1(mem8)] = x;
			}

			private void set_CR4(int newval)
			{
				cpu.cr4 = newval;
			}

			private void set_CR3(int new_pdb)
			{
				cpu.cr3 = new_pdb;
				if ((cpu.cr0 & (1 << 31)) != 0)
				{
					//if in paging mode must reset tables
					cpu.tlb_flush_all();
				}
			}

			private void set_CR0(uint Qd)
			{
				if ((Qd & (1 << 0)) == 0) //0th bit protected or real, real not supported!
					cpu_abort("real mode not supported");
				//if changing flags 31, 16, or 0 must flush tlb
				if ((Qd & ((1 << 31) | (1 << 16) | (1 << 0))) != (cpu.cr0 & ((1 << 31) | (1 << 16) | (1 << 0))))
				{
					cpu.tlb_flush_all();
				}
				cpu.cr0 = (int)(Qd | (1 << 4)); //keep bit 4 set to 1
			}

			private void stringOp_MOVSD()
			{
				int Xf;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				var Yf = regs[7];
				mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				var eg = ((Yf & Xf) + cpu.segs[0].@base) >> 0;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[REG_CX];
					if ((ag & Xf) == 0)
						return;
					if (Xf == -1 && cpu.df == 1 && ((mem8_loc | eg) & 3) == 0)
					{
						int i;
						var len = ag >> 0;
						var l = (4096 - (mem8_loc & 0xfff)) >> 2;
						if (len > l)
							len = l;
						l = (uint)((4096 - (eg & 0xfff)) >> 2);
						if (len > l)
							len = l;
						var ug = do_tlb_lookup(mem8_loc, 0);
						var vg = do_tlb_lookup((uint)eg, 1);
						var wg = len << 2;
						vg >>= 2;
						ug >>= 2;
						for (i = 0; i < len; i++)
							phys_mem32[vg + i] = phys_mem32[ug + i];
						regs[6] = (cg + wg) >> 0;
						regs[7] = (Yf + wg) >> 0;
						regs[REG_CX] = ag = (ag - len) >> 0;
						if (ag != 0)
							physmem8_ptr = initial_mem_ptr;
					}
					else
					{
						var x = ld_32bits_mem8_read();
						mem8_loc = (uint)eg;
						st32_mem8_write(x);
						regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
						regs[REG_CX] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
						if ((ag & Xf) != 0)
							physmem8_ptr = initial_mem_ptr;
					}
				}
				else
				{
					var x = ld_32bits_mem8_read();
					mem8_loc = (uint)eg;
					st32_mem8_write(x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
				}
			}

			private void DefaultDumpOpLog(uint OPbyte)
			{
				if (opLog.Logger.IsEnabledFor(Level.Trace))
				{
					opLog.Logger.Log(GetType(), Level.Trace, RenderDumpMsg(OPbyte), null);
				}
				if (cpu.TestLogEvent != null)
				{
					cpu.TestLogEvent(RenderDumpMsg(OPbyte));
				}
			}

			private string RenderDumpMsg(uint OPbyte)
			{
				var message = new StringBuilder();
				message.Append(" EIP: " + (int)eip_offset);
				message.Append(" ptr: " + (int)physmem8_ptr);
				message.Append(" mem: " + (int)mem8_loc);
				message.Append(" dst: " + (int)_dst);
				message.Append(" src: " + (int)_src);
				message.Append(" OP: " + (int)OPbyte);
				if (false && OPbyte == 0x0f)
					message.Append(" " + (int)phys_mem8[physmem8_ptr]);
				message.Append(" regs: [" + string.Join(",", regs.Select(x => (int)x)) + "]");
				return message.ToString();
			}

			private void stringOp_STOSD()
			{
				int Xf;
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				mem8_loc = (uint)((Yf & Xf) + cpu.segs[0].@base) >> 0;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[REG_CX];
					if ((ag & Xf) == 0)
						return;
					if (Xf == -1 && cpu.df == 1 && (mem8_loc & 3) == 0)
					{
						int i;
						var len = ag >> 0;
						var l = (4096 - (mem8_loc & 0xfff)) >> 2;
						if (len > l)
							len = l;
						var vg = do_tlb_lookup(regs[7], 1);
						var x = regs[REG_AX];
						vg >>= 2;
						for (i = 0; i < len; i++)
							phys_mem32[vg + i] = (int)x;
						var wg = len << 2;
						regs[7] = (Yf + wg) >> 0;
						regs[REG_CX] = ag = (ag - len) >> 0;
						if (ag != 0)
							physmem8_ptr = (uint)initial_mem_ptr;
					}
					else
					{
						st32_mem8_write(regs[REG_AX]);
						regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
						regs[REG_CX] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
						if ((ag & Xf) != 0)
							physmem8_ptr = (uint)initial_mem_ptr;
					}
				}
				else
				{
					st32_mem8_write(regs[REG_AX]);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
				}
			}

			private int do_tlb_lookup(uint mem8_loc, int ud)
			{
				int tlb_lookup;
				if (ud != 0)
				{
					tlb_lookup = _tlb_write_[mem8_loc >> 12];
				}
				else
				{
					tlb_lookup = _tlb_read_[mem8_loc >> 12];
				}
				if (tlb_lookup == -1)
				{
					do_tlb_set_page(mem8_loc, ud != 0, cpu.cpl == 3);
					if (ud != 0)
					{
						tlb_lookup = _tlb_write_[mem8_loc >> 12];
					}
					else
					{
						tlb_lookup = _tlb_read_[mem8_loc >> 12];
					}
				}
				return (int)(tlb_lookup ^ mem8_loc);
			}

			internal void set_segment_register(int register, int selector)
			{
				selector &= 0xffff;
				if ((cpu.cr0 & (1 << 0)) == 0)
				{
					//CR0.PE (0 == real mode)
					var descriptor_table = cpu.segs[register];
					descriptor_table.selector = selector;
					descriptor_table.@base = (uint)(selector << 4);
				}
				else if ((cpu.eflags & 0x00020000) != 0)
				{
					//EFLAGS.VM (1 == v86 mode)
					init_segment_vars_with_selector(register, selector);
				}
				else
				{
					//protected mode
					set_protected_mode_segment_register(register, selector);
				}
			}

			private void set_protected_mode_segment_register(int register, int selector)
			{
				int selector_index;
				var cpl_var = cpu.cpl;
				if ((selector & 0xfffc) == 0)
				{
					//null selector
					if (register == 2) //(SS == null) => #GP(0)
						abort_with_error_code(13, 0);
					set_segment_vars(register, selector, 0, 0, 0);
				}
				else
				{
					Segment descriptor_table;
					if ((selector & 0x4) != 0)
						descriptor_table = cpu.ldt;
					else
						descriptor_table = cpu.gdt;
					selector_index = selector & ~7;
					if ((selector_index + 7) > descriptor_table.limit)
						abort_with_error_code(13, selector & 0xfffc);
					mem8_loc = (uint)((descriptor_table.@base + selector_index) & -1);
					var descriptor_low4bytes = ld32_mem8_kernel_read();
					mem8_loc += 4;
					var descriptor_high4bytes = ld32_mem8_kernel_read();
					if ((descriptor_high4bytes & (1 << 12)) == 0)
						abort_with_error_code(13, selector & 0xfffc);
					var rpl = selector & 3;
					var dpl = (descriptor_high4bytes >> 13) & 3;
					if (register == 2)
					{
						if ((descriptor_high4bytes & (1 << 11)) != 0 || (descriptor_high4bytes & (1 << 9)) == 0)
							abort_with_error_code(13, selector & 0xfffc);
						if (rpl != cpl_var || dpl != cpl_var)
							abort_with_error_code(13, selector & 0xfffc);
					}
					else
					{
						if ((descriptor_high4bytes & ((1 << 11) | (1 << 9))) == (1 << 11))
							abort_with_error_code(13, selector & 0xfffc);
						if ((descriptor_high4bytes & (1 << 11)) == 0 || (descriptor_high4bytes & (1 << 10)) == 0)
						{
							if (dpl < cpl_var || dpl < rpl)
								abort_with_error_code(13, selector & 0xfffc);
						}
					}
					if ((descriptor_high4bytes & (1 << 15)) == 0)
					{
						if (register == 2)
							abort_with_error_code(12, selector & 0xfffc);
						else
							abort_with_error_code(11, selector & 0xfffc);
					}
					if ((descriptor_high4bytes & (1 << 8)) == 0)
					{
						descriptor_high4bytes |= (1 << 8);
						st32_mem8_kernel_write(descriptor_high4bytes);
					}
					set_segment_vars(register, selector, (uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes),
						calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes), descriptor_high4bytes);
				}
			}

			private void st32_mem8_kernel_write(int x)
			{
				var tlb_lookup = tlb_write_kernel[mem8_loc >> 12];
				if (((tlb_lookup | mem8_loc) & 3) != 0)
				{
					__st32_mem8_kernel_write(x);
				}
				else
				{
					phys_mem32[(mem8_loc ^ tlb_lookup) >> 2] = x;
				}
			}

			private void __st32_mem8_kernel_write(int x)
			{
				st8_mem8_kernel_write(x);
				mem8_loc++;
				st8_mem8_kernel_write(x >> 8);
				mem8_loc++;
				st8_mem8_kernel_write(x >> 16);
				mem8_loc++;
				st8_mem8_kernel_write(x >> 24);
				mem8_loc -= 3;
			}

			private void st8_mem8_kernel_write(int x)
			{
				var tlb_lookup = tlb_write_kernel[mem8_loc >> 12];
				if (tlb_lookup == -1)
				{
					__st8_mem8_kernel_write(x);
				}
				else
				{
					phys_mem8[mem8_loc ^ tlb_lookup] = (byte)x;
				}
			}

			private void __st8_mem8_kernel_write(int x)
			{
				do_tlb_set_page(mem8_loc, true, false);
				var tlb_lookup = tlb_write_kernel[mem8_loc >> 12] ^ mem8_loc;
				phys_mem8[tlb_lookup] = (byte)x;
			}

			private void init_segment_vars_with_selector(int register, int selector)
			{
				throw new NotImplementedException();
			}

			private void op_DAS()
			{
				var flag_bits = get_conditional_flags();
				var Ef = flag_bits & 0x0001;
				var Bf = flag_bits & 0x0010;
				var wf = regs[REG_AX] & 0xff;
				flag_bits = 0;
				var Gf = wf;
				if (((wf & 0x0f) > 9) || Bf != 0)
				{
					flag_bits |= 0x0010;
					if (wf < 6 || Ef != 0)
						flag_bits |= 0x0001;
					wf = (wf - 6) & 0xff;
				}
				if ((Gf > 0x99) || Ef != 0)
				{
					wf = (wf - 0x60) & 0xff;
					flag_bits |= 0x0001;
				}
				regs[REG_AX] = (uint)((regs[REG_AX] & ~0xff) | wf);
				flag_bits = (uint)(flag_bits | (wf == 0 ? 1 : 0) << 6);
				flag_bits = (uint)(flag_bits | cpu.parity_LUT[wf] << 2);
				flag_bits |= (wf & 0x80);
				u_src = flag_bits;
				u_dst = ((u_src >> 6) & 1) ^ 1;
				_op = 24;
			}

			private void stringOp_OUTSD()
			{
				int Xf;
				int x;
				var iopl = (cpu.eflags >> 12) & 3;
				if (cpu.cpl > iopl)
					abort(13);
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Sb = CS_flags & 0x000f;
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				var cg = regs[6];
				var Zf = regs[2] & 0xffff;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[REG_CX];
					if ((ag & Xf) == 0)
						return;
					mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
					x = (int)ld_32bits_mem8_read();
					cpu.st32_port(Zf, (uint)x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
					regs[REG_CX] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((ag & Xf) != 0)
						physmem8_ptr = (uint)initial_mem_ptr;
				}
				else
				{
					mem8_loc = (uint)(((cg & Xf) + cpu.segs[Sb].@base) >> 0);
					x = (int)ld_32bits_mem8_read();
					cpu.st32_port(Zf, (uint)x);
					regs[6] = (uint)((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
				}
			}

			private void stringOp_INSB()
			{
				int Xf;
				int x;
				var iopl = (cpu.eflags >> 12) & 3;
				if (cpu.cpl > iopl)
					abort(13);
				if ((CS_flags & 0x0080) != 0)
					Xf = 0xffff;
				else
					Xf = -1;
				var Yf = regs[7];
				var Zf = regs[2] & 0xffff;
				if ((CS_flags & (0x0010 | 0x0020)) != 0)
				{
					var ag = regs[REG_CX];
					if ((ag & Xf) == 0)
						return;
					x = cpu.ld8_port(Zf);
					mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
					st8_mem8_write((uint)x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
					regs[REG_CX] = ag = (uint)((ag & ~Xf) | ((ag - 1) & Xf));
					if ((ag & Xf) != 0)
						physmem8_ptr = (uint)initial_mem_ptr;
				}
				else
				{
					x = cpu.ld8_port(Zf);
					mem8_loc = (uint)(((Yf & Xf) + cpu.segs[0].@base) >> 0);
					st8_mem8_write((uint)x);
					regs[7] = (uint)((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				}
			}

			internal bool check_less_than()
			{
				bool result;
				switch (_op)
				{
					case 6:
						result = ((_dst + u_src) << 24) < (u_src << 24);
						break;
					case 7:
						result = ((u_dst + u_src) << 16) < (u_src << 16);
						break;
					case 8:
						result = (_dst + u_src) < u_src;
						break;
					case 12:
					case 25:
					case 28:
					case 13:
					case 26:
					case 29:
					case 14:
					case 27:
					case 30:
						result = _dst < 0;
						break;
					case 24:
						result = (((u_src >> 7) ^ (u_src >> 11)) & 1) != 0;
						break;
					default:
						result = ((_op == 24 ? ((u_src >> 7) & 1) : (uint)(_dst < 0 ? 1 : 0)) ^ check_overflow()) != 0;
						break;
				}
				return result;
			}

			internal bool check_below_or_equal()
			{
				bool result;
				switch (_op)
				{
					case 6:
						result = ((u_dst + u_src) & 0xff) <= (u_src & 0xff);
						break;
					case 7:
						result = ((u_dst + u_src) & 0xffff) <= (u_src & 0xffff);
						break;
					case 8:
						result = ((u_dst + u_src) >> 0) <= (u_src >> 0);
						break;
					case 24:
						result = (u_src & (0x0040 | 0x0001)) != 0;
						break;
					default:
						result = check_carry() != 0 || (u_dst == 0);
						break;
				}
				return result;
			}

			private void op_ARPL()
			{
				int mem8;
				int x;
				int y;
				var reg_idx0 = 0;
				if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
					abort(6);
				mem8 = phys_mem8[physmem8_ptr++];
				if (isRegisterAddressingMode)
				{
					reg_idx0 = regIdx0(mem8);
					x = (int)(regs[reg_idx0] & 0xffff);
				}
				else
				{
					mem8_loc = segment_translation(mem8);
					x = ld_16bits_mem8_write();
				}
				y = (int)regs[regIdx1(mem8)];
				u_src = get_conditional_flags();
				if ((x & 3) < (y & 3))
				{
					x = (x & ~3) | (y & 3);
					if (isRegisterAddressingMode)
					{
						set_lower_word_in_register(reg_idx0, (uint)x);
					}
					else
					{
						st16_mem8_write((uint)x);
					}
					u_src |= 0x0040;
				}
				else
				{
					u_src = (uint)(u_src & ~0x0040);
				}
				u_dst = ((u_src >> 6) & 1) ^ 1;
				_op = 24;
			}

			private uint get_conditional_flags()
			{
				return (uint)((check_carry() << 0)
							   | (check_parity() << 2)
							   | ((u_dst == 0 ? 1 : 0) << 6)
							   | ((_op == 24 ? (int)((_src >> 7) & 1) : ((int)_dst < 0 ? 1 : 0)) << 7)
							   | (check_overflow() << 11)
							   | check_adjust_flag());
			}

			private uint check_adjust_flag()
			{
				uint Yb;
				uint result;
				switch (_op)
				{
					case 0:
					case 1:
					case 2:
						Yb = (u_dst - u_src) >> 0;
						result = (u_dst ^ Yb ^ u_src) & 0x10;
						break;
					case 3:
					case 4:
					case 5:
						Yb = (u_dst - u_src - 1) >> 0;
						result = (u_dst ^ Yb ^ u_src) & 0x10;
						break;
					case 6:
					case 7:
					case 8:
						Yb = (u_dst + u_src) >> 0;
						result = (u_dst ^ Yb ^ u_src) & 0x10;
						break;
					case 9:
					case 10:
					case 11:
						Yb = (u_dst + u_src + 1) >> 0;
						result = (u_dst ^ Yb ^ u_src) & 0x10;
						break;
					case 12:
					case 13:
					case 14:
						result = 0;
						break;
					case 15:
					case 18:
					case 16:
					case 19:
					case 17:
					case 20:
					case 21:
					case 22:
					case 23:
						result = 0;
						break;
					case 24:
						result = u_src & 0x10;
						break;
					case 25:
					case 26:
					case 27:
						result = (u_dst ^ (u_dst - 1)) & 0x10;
						break;
					case 28:
					case 29:
					case 30:
						result = (u_dst ^ (u_dst + 1)) & 0x10;
						break;
					default:
						throw new Exception("AF: unsupported cc_op=" + _op);
				}
				return result;
			}

			internal long check_parity()
			{
				if (_op == 24)
				{
					return (u_src >> 2) & 1;
				}
				else
				{
					return cpu.parity_LUT[u_dst & 0xff];
				}
			}

			private int ld_16bits_mem8_write()
			{
				int tlb_lookup;
				return (((tlb_lookup = _tlb_write_[mem8_loc >> 12]) | mem8_loc) & 1) != 0
					? __ld_16bits_mem8_write()
					: (int)phys_mem16[(mem8_loc ^ tlb_lookup) >> 1];
			}

			private int __ld_16bits_mem8_write()
			{
				var x = ld_8bits_mem8_write();
				mem8_loc++;
				x |= ld_8bits_mem8_write() << 8;
				mem8_loc--;
				return (int)x;
			}

			private void op_JMPF(uint selector, uint Le)
			{
				if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
				{
					do_JMPF_virtual_mode(selector, Le);
				}
				else
				{
					do_JMPF(selector, Le);
				}
			}

			private void do_JMPF(uint selector, uint Le)
			{
				uint Ne;
				uint ie;
				int descriptor_low4bytes;
				int descriptor_high4bytes;
				int cpl_var;
				int dpl;
				uint rpl;
				int limit;
				int[] e;
				if ((selector & 0xfffc) == 0)
					abort_with_error_code(13, 0);
				e = load_from_descriptor_table(selector);
				if (e == null)
					abort_with_error_code(13, (int)(selector & 0xfffc));
				descriptor_low4bytes = e[0];
				descriptor_high4bytes = e[1];
				cpl_var = cpu.cpl;
				if ((descriptor_high4bytes & (1 << 12)) != 0)
				{
					if ((descriptor_high4bytes & (1 << 11)) == 0)
						abort_with_error_code(13, (int)(selector & 0xfffc));
					dpl = (descriptor_high4bytes >> 13) & 3;
					if ((descriptor_high4bytes & (1 << 10)) != 0)
					{
						if (dpl > cpl_var)
							abort_with_error_code(13, (int)(selector & 0xfffc));
					}
					else
					{
						rpl = selector & 3;
						if (rpl > cpl_var)
							abort_with_error_code(13, (int)(selector & 0xfffc));
						if (dpl != cpl_var)
							abort_with_error_code(13, (int)(selector & 0xfffc));
					}
					if ((descriptor_high4bytes & (1 << 15)) == 0)
						abort_with_error_code(11, (int)(selector & 0xfffc));
					limit = calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes);
					if ((Le >> 0) > (uint)(limit >> 0))
						abort_with_error_code(13, (int)(selector & 0xfffc));
					set_segment_vars(1, (int)((selector & 0xfffc) | cpl_var),
						(uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes), (int)limit,
						(int)descriptor_high4bytes);
					eip = Le;
					physmem8_ptr = 0;
					initial_mem_ptr = 0;
				}
				else
				{
					cpu_abort("unsupported jump to call or task gate");
				}
			}

			private void cpu_abort(string unsupportedJumpToCallOrTaskGate)
			{
				throw new NotImplementedException();
			}

			private void set_segment_vars(int ee, int selector, uint @base, int limit, int flags)
			{
				cpu.segs[ee] = new Segment { selector = selector, @base = @base, limit = limit, flags = flags };
				init_segment_local_vars();
			}

			private int calculate_descriptor_base(int descriptor_low4bytes, int descriptor_high4bytes)
			{
				return
					(int)
						((((descriptor_low4bytes >> 16) | ((descriptor_high4bytes & 0xff) << 16) | (descriptor_high4bytes & 0xff000000))) &
						 -1);
			}

			private int calculate_descriptor_limit(int descriptor_low4bytes, int descriptor_high4bytes)
			{
				var limit = (descriptor_low4bytes & 0xffff) | (descriptor_high4bytes & 0x000f0000);
				if ((descriptor_high4bytes & (1 << 23)) != 0)
					limit = (limit << 12) | 0xfff;
				return limit;
			}

			private int[] load_from_descriptor_table(uint selector)
			{
				Segment descriptor_table;
				if ((selector & 0x4) != 0)
					descriptor_table = cpu.ldt;
				else
					descriptor_table = cpu.gdt;
				var Rb = selector & ~7;
				if ((Rb + 7) > descriptor_table.limit)
					return null;
				mem8_loc = (uint)(descriptor_table.@base + Rb);
				var descriptor_low4bytes = ld32_mem8_kernel_read();
				mem8_loc += 4;
				var descriptor_high4bytes = ld32_mem8_kernel_read();
				return new[] { descriptor_low4bytes, descriptor_high4bytes };
			}

			private int ld32_mem8_kernel_read()
			{
				uint tlb_lookup;
				return (((tlb_lookup = (uint)tlb_read_kernel[mem8_loc >> 12]) | mem8_loc) & 3) != 0
					? __ld32_mem8_kernel_read()
					: phys_mem32[(mem8_loc ^ tlb_lookup) >> 2];
			}

			private int __ld32_mem8_kernel_read()
			{
				var x = ld8_mem8_kernel_read();
				mem8_loc++;
				x |= ld8_mem8_kernel_read() << 8;
				mem8_loc++;
				x |= ld8_mem8_kernel_read() << 16;
				mem8_loc++;
				x |= ld8_mem8_kernel_read() << 24;
				mem8_loc -= 3;
				return x;
			}

			private int ld8_mem8_kernel_read()
			{
				int tlb_lookup;
				return ((tlb_lookup = tlb_read_kernel[mem8_loc >> 12]) == -1)
					? __ld8_mem8_kernel_read()
					: phys_mem8[mem8_loc ^ tlb_lookup];
			}

			private int __ld8_mem8_kernel_read()
			{
				do_tlb_set_page(mem8_loc, false, false);
				var tlb_lookup = tlb_read_kernel[mem8_loc >> 12] ^ mem8_loc;
				return phys_mem8[tlb_lookup];
			}

			private void do_JMPF_virtual_mode(uint selector, uint le)
			{
				throw new NotImplementedException();
			}

			private void tlb_flush_page(long mem8_loc)
			{
				var i = mem8_loc >> 12;
				this.tlb_read_kernel[i] = -1;
				this.tlb_write_kernel[i] = -1;
				this.tlb_read_user[i] = -1;
				this.tlb_write_user[i] = -1;
			}

			private uint ld_16bits_mem8_read()
			{
				uint last_tlb_val;
				return ((((last_tlb_val = (uint)_tlb_read_[mem8_loc >> 12]) | mem8_loc) & 1) != 0
					? __ld_16bits_mem8_read()
					: phys_mem16[(mem8_loc ^ last_tlb_val) >> 1]);
			}

			private uint __ld_16bits_mem8_read()
			{
				var x = ld_8bits_mem8_read();
				mem8_loc++;
				x |= ld_8bits_mem8_read() << 8;
				mem8_loc--;
				return x;
			}

			internal bool check_less_or_equal()
			{
				bool result;
				switch (_op)
				{
					case 6:
						result = ((u_dst + u_src) << 24) <= (u_src << 24);
						break;
					case 7:
						result = ((u_dst + u_src) << 16) <= (u_src << 16);
						break;
					case 8:
						result = (((int)_dst + (int)_src) >> 0) <= (int)_src;
						break;
					case 12:
					case 25:
					case 28:
					case 13:
					case 26:
					case 29:
					case 14:
					case 27:
					case 30:
						result = (int)u_dst <= 0;
						break;
					case 24:
						result = ((((u_src >> 7) ^ (u_src >> 11)) | (u_src >> 6)) & 1) != 0;
						break;
					default:
						result = ((_op == 24 ? (_src >> 7) & 1 : ((int)_dst < 0 ? 1 : 0)) ^ check_overflow()) != 0 | (u_dst == 0);
						break;
				}
				return result;
			}

			internal int check_overflow()
			{
				long result;
				uint Yb;
				switch (_op)
				{
					case 0:
						Yb = (u_dst - u_src) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 7) & 1;
						break;
					case 1:
						Yb = (u_dst - u_src) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 15) & 1;
						break;
					case 2:
						Yb = (u_dst - u_src) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 31) & 1;
						break;
					case 3:
						Yb = (u_dst - u_src - 1) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 7) & 1;
						break;
					case 4:
						Yb = (u_dst - u_src - 1) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 15) & 1;
						break;
					case 5:
						Yb = (u_dst - u_src - 1) >> 0;
						result = (((Yb ^ u_src ^ -1) & (Yb ^ u_dst)) >> 31) & 1;
						break;
					case 6:
						Yb = (u_dst + u_src) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 7) & 1;
						break;
					case 7:
						Yb = (u_dst + u_src) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 15) & 1;
						break;
					case 8:
						Yb = (u_dst + u_src) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 31) & 1;
						break;
					case 9:
						Yb = (u_dst + u_src + 1) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 7) & 1;
						break;
					case 10:
						Yb = (u_dst + u_src + 1) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 15) & 1;
						break;
					case 11:
						Yb = (u_dst + u_src + 1) >> 0;
						result = (((Yb ^ u_src) & (Yb ^ u_dst)) >> 31) & 1;
						break;
					case 12:
					case 13:
					case 14:
						result = 0;
						break;
					case 15:
					case 18:
						result = ((u_src ^ u_dst) >> 7) & 1;
						break;
					case 16:
					case 19:
						result = ((u_src ^ u_dst) >> 15) & 1;
						break;
					case 17:
					case 20:
						result = ((u_src ^ u_dst) >> 31) & 1;
						break;
					case 21:
					case 22:
					case 23:
						result = u_src != 0 ? 1 : 0;
						break;
					case 24:
						result = (u_src >> 11) & 1;
						break;
					case 25:
						result = (u_dst & 0xff) == 0x80 ? 1 : 0;
						break;
					case 26:
						result = (u_dst & 0xffff) == 0x8000 ? 1 : 0;
						break;
					case 27:
						result = (u_dst == -2147483648) ? 1 : 0;
						break;
					case 28:
						result = (u_dst & 0xff) == 0x7f ? 1 : 0;
						break;
					case 29:
						result = (u_dst & 0xffff) == 0x7fff ? 1 : 0;
						break;
					case 30:
						result = u_dst == 0x7fffffff ? 1 : 0;
						break;
					default:
						throw new Exception("JO: unsupported cc_op=" + _op);
				}
				return (int)result;
			}

			private void st16_mem8_write(uint x)
			{
				{
					var last_tlb_val = _tlb_write_[mem8_loc >> 12];
					if (((last_tlb_val | mem8_loc) & 1) != 0)
					{
						__st16_mem8_write(x);
					}
					else
					{
						phys_mem16[(mem8_loc ^ last_tlb_val) >> 1] = (ushort)x;
					}
				}
			}

			private void __st16_mem8_write(uint x)
			{
				st8_mem8_write(x);
				mem8_loc++;
				st8_mem8_write(x >> 8);
				mem8_loc--;
			}

			private void set_lower_word_in_register(int reg_idx1, uint x)
			{
				regs[reg_idx1] = (uint)((regs[reg_idx1] & -65536) | (x & 0xffff));
			}

			private uint segmented_mem8_loc_for_MOV()
			{
				uint mem8_loc;
				int Sb;
				if ((CS_flags & 0x0080) != 0)
					mem8_loc = (uint)ld16_mem8_direct();
				else
					mem8_loc = phys_mem8_uint();
				Sb = (int)(CS_flags & 0x000f);
				if (Sb == 0)
					Sb = 3;
				else
					Sb--;
				mem8_loc = (mem8_loc + cpu.segs[Sb].@base) >> 0;
				return mem8_loc;
			}

			/// <summary>
			/// used only for errors 0, 5, 6, 7, 13
			/// </summary>
			/// <param name="i"></param>
			private void abort(int intno)
			{
				abort_with_error_code(intno, 0);
			}

			private uint shift32(int conditional_var, uint Yb, int Zb)
			{
				uint kc;
				uint ac;
				switch (conditional_var)
				{
					case 0:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							kc = Yb;
							Yb = (Yb << Zb) | (Yb >> (32 - Zb));
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (Yb & 0x0001) | (((kc ^ Yb) >> 20) & 0x0800);
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 1:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							kc = Yb;
							Yb = (Yb >> Zb) | (Yb << (32 - Zb));
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= ((Yb >> 31) & 0x0001) | (((kc ^ Yb) >> 20) & 0x0800);
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 2:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							kc = Yb;
							ac = check_carry();
							Yb = (uint)((Yb << Zb) | (ac << (Zb - 1)));
							if (Zb > 1)
								Yb |= (uint)kc >> (33 - Zb);
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (uint)((((kc ^ Yb) >> 20) & 0x0800) | ((kc >> (32 - Zb)) & 0x0001));
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 3:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							kc = Yb;
							ac = check_carry();
							Yb = (uint)((Yb >> Zb) | (ac << (32 - Zb)));
							if (Zb > 1)
								Yb |= (uint)kc << (33 - Zb);
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (uint)((((kc ^ Yb) >> 20) & 0x0800) | ((kc >> (Zb - 1)) & 0x0001));
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 4:
					case 6:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							u_src = Yb << (Zb - 1);
							u_dst = Yb = Yb << Zb;
							_op = 17;
						}
						break;
					case 5:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							u_src = Yb >> (Zb - 1);
							u_dst = Yb = Yb >> Zb;
							_op = 20;
						}
						break;
					case 7:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							_src = (int)Yb >> (Zb - 1);
							_dst = (int) (Yb = (uint) ((int)Yb >> Zb));
							_op = 20;
						}
						break;
					default:
						throw new Exception("unsupported shift32=" + conditional_var);
				}
				return Yb;
			}

			private uint shift8(int conditional_var, uint Yb, int Zb)
			{
				uint kc;
				uint ac;
				switch (conditional_var)
				{
					case 0:
						if ((Zb & 0x1f) != 0)
						{
							Zb &= 0x7;
							Yb &= 0xff;
							kc = Yb;
							Yb = (Yb << Zb) | (Yb >> (8 - Zb));
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (Yb & 0x0001) | (((kc ^ Yb) << 4) & 0x0800);
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 1:
						if ((Zb & 0x1f) != 0)
						{
							Zb &= 0x7;
							Yb &= 0xff;
							kc = Yb;
							Yb = (Yb >> Zb) | (Yb << (8 - Zb));
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= ((Yb >> 7) & 0x0001) | (((kc ^ Yb) << 4) & 0x0800);
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 2:
						Zb = cpu.shift8_LUT[Zb & 0x1f];
						if (Zb != 0)
						{
							Yb &= 0xff;
							kc = Yb;
							ac = check_carry();
							Yb = (uint)((Yb << Zb) | (ac << (Zb - 1)));
							if (Zb > 1)
								Yb |= (uint)kc >> (9 - Zb);
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (uint)((((kc ^ Yb) << 4) & 0x0800) | ((kc >> (8 - Zb)) & 0x0001));
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 3:
						Zb = cpu.shift8_LUT[Zb & 0x1f];
						if (Zb != 0)
						{
							Yb &= 0xff;
							kc = Yb;
							ac = check_carry();
							Yb = (uint)((Yb >> Zb) | (ac << (8 - Zb)));
							if (Zb > 1)
								Yb |= (uint)kc << (9 - Zb);
							u_src = conditional_flags_for_rot_shift_ops();
							u_src |= (uint)((((kc ^ Yb) << 4) & 0x0800) | ((kc >> (Zb - 1)) & 0x0001));
							u_dst = ((u_src >> 6) & 1) ^ 1;
							_op = 24;
						}
						break;
					case 4:
					case 6:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							u_src = Yb << (Zb - 1);
							u_dst = Yb = (((Yb << Zb) << 24) >> 24);
							_op = 15;
						}
						break;
					case 5:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							Yb &= 0xff;
							u_src = Yb >> (Zb - 1);
							u_dst = Yb = (((Yb >> Zb) << 24) >> 24);
							_op = 18;
						}
						break;
					case 7:
						Zb &= 0x1f;
						if (Zb != 0)
						{
							Yb = (Yb << 24) >> 24;
							u_src = Yb >> (Zb - 1);
							u_dst = Yb = (((Yb >> Zb) << 24) >> 24);
							_op = 18;
						}
						break;
					default:
						throw new Exception("unsupported shift8=" + conditional_var);
				}
				return Yb;
			}

			private uint conditional_flags_for_rot_shift_ops()
			{
				return (uint)((check_parity() << 2) | ((_dst == 0 ? 1 : 0) << 6) | ((_op == 24 ? ((_src >> 7) & 1) : (_dst < 0 ? 1 : 0)) << 7) | check_adjust_flag());
			}

			internal void pop_dword_from_stack_incr_ptr()
			{
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((regs[4] + 4) & SS_mask));
			}

			internal uint pop_dword_from_stack_read()
			{
				mem8_loc = (uint)(((regs[4] & SS_mask) + SS_base) >> 0);
				return ld_32bits_mem8_read();
			}

			internal uint __ld_32bits_mem8_read()
			{
				var x = ld_8bits_mem8_read();
				mem8_loc++;
				x |= ld_8bits_mem8_read() << 8;
				mem8_loc++;
				x |= ld_8bits_mem8_read() << 16;
				mem8_loc++;
				x |= ld_8bits_mem8_read() << 24;
				mem8_loc -= 3;
				return x;
			}

			private uint ld_32bits_mem8_write()
			{
				var x = ld_8bits_mem8_write();
				mem8_loc++;
				x |= ld_8bits_mem8_write() << 8;
				mem8_loc++;
				x |= ld_8bits_mem8_write() << 16;
				mem8_loc++;
				x |= ld_8bits_mem8_write() << 24;
				mem8_loc -= 3;
				return x;
			}

			private uint do_32bit_math(int conditional_var, uint Yb, uint Zb)
			{
				uint ac = 0;
				switch (conditional_var)
				{
					case 0:
						u_src = Zb;
						Yb = (Yb + Zb) >> 0;
						u_dst = Yb;
						_op = 2;
						break;
					case 1:
						Yb = Yb | Zb;
						u_dst = Yb;
						_op = 14;
						break;
					case 2:
						ac = check_carry();
						u_src = Zb;
						Yb = (Yb + Zb + ac) >> 0;
						u_dst = Yb;
						_op = ac != 0 ? 5 : 2;
						break;
					case 3:
						ac = check_carry();
						u_src = Zb;
						Yb = (Yb - Zb - ac) >> 0;
						u_dst = Yb;
						_op = ac != 0 ? 11 : 8;
						break;
					case 4:
						Yb = Yb & Zb;
						u_dst = Yb;
						_op = 14;
						break;
					case 5:
						u_src = Zb;
						Yb = (Yb - Zb) >> 0;
						u_dst = Yb;
						_op = 8;
						break;
					case 6:
						Yb = Yb ^ Zb;
						u_dst = Yb;
						_op = 14;
						break;
					case 7:
						u_src = Zb;
						u_dst = (Yb - Zb) >> 0;
						_op = 8;
						break;
					default:
						throw new Exception("arith" + conditional_var + ": invalid op");
				}
				return Yb;
			}

			/// <summary>
			/// Status bits and Flags Routines
			/// </summary>
			/// <returns></returns>
			internal uint check_carry()
			{
				int Yb;
				bool result;
				int current_op;
				int relevant_dst;
				if (_op >= 25)
				{
					current_op = _op2;
					relevant_dst = _dst2;
				}
				else
				{
					current_op = _op;
					relevant_dst = (int)u_dst;
				}
				switch (current_op)
				{
					case 0:
						result = (relevant_dst & 0xff) < (u_src & 0xff);
						break;
					case 1:
						result = (relevant_dst & 0xffff) < (u_src & 0xffff);
						break;
					case 2:
						result = ((uint)relevant_dst) < ((uint)u_src);
						break;
					case 3:
						result = (relevant_dst & 0xff) <= (u_src & 0xff);
						break;
					case 4:
						result = (relevant_dst & 0xffff) <= (u_src & 0xffff);
						break;
					case 5:
						result = (relevant_dst >> 0) <= (u_src >> 0);
						break;
					case 6:
						result = ((relevant_dst + u_src) & 0xff) < (u_src & 0xff);
						break;
					case 7:
						result = ((relevant_dst + u_src) & 0xffff) < (u_src & 0xffff);
						break;
					case 8:
						result = (uint)(relevant_dst + _src) < u_src;
						break;
					case 9:
						Yb = (int)((relevant_dst + u_src + 1) & 0xff);
						result = Yb <= (u_src & 0xff);
						break;
					case 10:
						Yb = (int)((relevant_dst + u_src + 1) & 0xffff);
						result = Yb <= (u_src & 0xffff);
						break;
					case 11:
						Yb = (int)((relevant_dst + u_src + 1) >> 0);
						result = Yb <= (u_src >> 0);
						break;
					case 12:
					case 13:
					case 14:
						result = false;
						break;
					case 15:
						result = ((u_src >> 7) & 1) != 0;
						break;
					case 16:
						result = ((u_src >> 15) & 1) != 0;
						break;
					case 17:
						result = ((u_src >> 31) & 1) != 0;
						break;
					case 18:
					case 19:
					case 20:
						result = (u_src & 1) != 0;
						break;
					case 21:
					case 22:
					case 23:
						result = u_src != 0;
						break;
					case 24:
						result = (u_src & 1) != 0;
						break;
					default:
						throw new Exception("GET_CARRY: unsupported cc_op=" + _op);
				}
				return (uint)(result ? 1 : 0);
			}

			private uint ld_32bits_mem8_read()
			{
				int last_tlb_val;
				return ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
					? __ld_32bits_mem8_read()
					: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
			}

			internal void st32_mem8_write(uint x)
			{
				var last_tlb_val = _tlb_write_[mem8_loc >> 12];
				if (((last_tlb_val | mem8_loc) & 3) != 0)
				{
					__st32_mem8_write(x);
				}
				else
				{
					phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;
				}
			}

			public void push_dword_to_stack(uint x)
			{
				var wd = regs[4] - 4;
				mem8_loc = (uint)(((wd & SS_mask) + SS_base) >> 0);
				st32_mem8_write(x);
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((wd) & SS_mask));
			}

			public void __st32_mem8_write(uint x)
			{
				st8_mem8_write(x);
				mem8_loc++;
				st8_mem8_write(x >> 8);
				mem8_loc++;
				st8_mem8_write(x >> 16);
				mem8_loc++;
				st8_mem8_write(x >> 24);
				mem8_loc -= 3;
			}

			internal uint ld_8bits_mem8_read()
			{
				var last_tlb_val = _tlb_read_[mem8_loc >> 12];
				return ((last_tlb_val == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
			}

			internal void st8_mem8_write(uint x)
			{
				st8_mem8_write((byte)x);
			}

			private void st8_mem8_write(byte x)
			{
				int last_tlb_val;
				{
					last_tlb_val = _tlb_write_[mem8_loc >> 12];
					if (last_tlb_val == -1)
					{
						__st8_mem8_write(x);
					}
					else
					{
						phys_mem8[mem8_loc ^ last_tlb_val] = (byte)x;
					}
				}
			}

			private void __st8_mem8_write(byte x)
			{
				do_tlb_set_page(mem8_loc, true, cpu.cpl == 3);
				var tlb_lookup = _tlb_write_[mem8_loc >> 12] ^ mem8_loc;
				phys_mem8[tlb_lookup] = x;
			}

			internal uint ld_8bits_mem8_write()
			{
				var tlb_lookup = _tlb_write_[mem8_loc >> 12];
				return ((tlb_lookup) == -1) ? __ld_8bits_mem8_write() : phys_mem8[mem8_loc ^ tlb_lookup];
			}

			private uint __ld_8bits_mem8_write()
			{
				do_tlb_set_page(mem8_loc, true, cpu.cpl == 3);
				var tlb_lookup = _tlb_write_[mem8_loc >> 12] ^ mem8_loc;
				return phys_mem8[tlb_lookup];
			}

			private uint eip;
			internal uint physmem8_ptr;
			private uint initial_mem_ptr;
			private uint eip_offset;
			internal int[] _tlb_read_;
			internal int _op2;
			internal int _dst2;

			private uint N_cycles;
			private uint cycles_left;
			private readonly Action<uint> DumpOpLog;

			public Executor(CPU_X86_Impl cpu)
				: this()
			{
				this.cpu = cpu;
				if (cpu.isDumpEnabled)
					DumpOpLog = DefaultDumpOpLog;
				else
					DumpOpLog = (x) => { };
			}

			internal void segment_translation()
			{
				mem8_loc = segment_translation(mem8);
			}

			/// <summary>
			/// segment translation routine (I believe):
			/// Translates Logical Memory Address to Linear Memory Address
			/// </summary>
			private uint segment_translation(int mem8)
			{
				int @base;
				uint mem8_loc = 0;
				int Qb;
				int Rb;
				int Sb;
				int Tb;
				if (FS_usage_flag && (CS_flags & (0x000f | 0x0080)) == 0)
				{
					switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
					{
						case 0x04:
							Qb = phys_mem8[physmem8_ptr++];
							@base = Qb & 7;
							if (@base == 5)
								mem8_loc = phys_mem8_uint();
							else
								mem8_loc = regs[@base];
							Rb = regIdx1(Qb);
							if (Rb != 4)
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							break;
						case 0x0c:
							Qb = phys_mem8[physmem8_ptr++];
							mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							@base = Qb & 7;
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							Rb = regIdx1(Qb);
							if (Rb != 4)
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							break;
						case 0x14:
							Qb = phys_mem8[physmem8_ptr++];
							mem8_loc = phys_mem8_uint();
							@base = Qb & 7;
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							Rb = regIdx1(Qb);
							if (Rb != 4)
							{
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							}
							break;
						case 0x05:
							mem8_loc = phys_mem8_uint();
							break;
						case 0x00:
						case 0x01:
						case 0x02:
						case 0x03:
						case 0x06:
						case 0x07:
							@base = regIdx0(mem8);
							mem8_loc = regs[@base];
							break;
						case 0x08:
						case 0x09:
						case 0x0a:
						case 0x0b:
						case 0x0d:
						case 0x0e:
						case 0x0f:
							mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							@base = regIdx0(mem8);
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							break;
						case 0x10:
						case 0x11:
						case 0x12:
						case 0x13:
						case 0x15:
						case 0x16:
						case 0x17:
						default:
							mem8_loc = phys_mem8_uint();
							@base = regIdx0(mem8);
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							break;
					}
					return (uint)mem8_loc;
				}
				else if ((CS_flags & 0x0080) != 0)
				{
					if ((mem8 & 0xc7) == 0x06)
					{
						mem8_loc = (uint)ld16_mem8_direct();
						Tb = 3;
					}
					else
					{
						switch(MOD)
						{
							case 0:
								mem8_loc = 0;
								break;
							case 1:
								mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								break;
							default:
								mem8_loc = (uint)ld16_mem8_direct();
								break;
						}
						switch (regIdx0(mem8))
						{
							case 0:
								mem8_loc = ((mem8_loc + regs[REG_BX] + regs[6]) & 0xffff);
								Tb = 3;
								break;
							case 1:
								mem8_loc = ((mem8_loc + regs[REG_BX] + regs[7]) & 0xffff);
								Tb = 3;
								break;
							case 2:
								mem8_loc = ((mem8_loc + regs[5] + regs[6]) & 0xffff);
								Tb = 2;
								break;
							case 3:
								mem8_loc = ((mem8_loc + regs[5] + regs[7]) & 0xffff);
								Tb = 2;
								break;
							case 4:
								mem8_loc = ((mem8_loc + regs[6]) & 0xffff);
								Tb = 3;
								break;
							case 5:
								mem8_loc = ((mem8_loc + regs[7]) & 0xffff);
								Tb = 3;
								break;
							case 6:
								mem8_loc = ((mem8_loc + regs[5]) & 0xffff);
								Tb = 2;
								break;
							case 7:
							default:
								mem8_loc = ((mem8_loc + regs[REG_BX]) & 0xffff);
								Tb = 3;
								break;
						}
					}
					Sb = (int)(CS_flags & 0x000f);
					if (Sb == 0)
					{
						Sb = (int)Tb;
					}
					else
					{
						Sb--;
					}
					mem8_loc = ((mem8_loc + cpu.segs[Sb].@base) >> 0);
					return mem8_loc;
				}
				else
				{
					switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
					{
						case 0x04:
							Qb = phys_mem8[physmem8_ptr++];
							@base = Qb & 7;
							if (@base == 5)
							{
								mem8_loc = phys_mem8_uint();
								@base = 0;
							}
							else
							{
								mem8_loc = regs[@base];
							}
							Rb = regIdx1(Qb);
							if (Rb != 4)
							{
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							}
							break;
						case 0x0c:
							Qb = phys_mem8[physmem8_ptr++];
							mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							@base = Qb & 7;
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							Rb = regIdx1(Qb);
							if (Rb != 4)
							{
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							}
							break;
						case 0x14:
							Qb = phys_mem8[physmem8_ptr++];
							mem8_loc = phys_mem8_uint();
							@base = Qb & 7;
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							Rb = regIdx1(Qb);
							if (Rb != 4)
							{
								mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
							}
							break;
						case 0x05:
							mem8_loc = phys_mem8_uint();
							@base = 0;
							break;
						case 0x00:
						case 0x01:
						case 0x02:
						case 0x03:
						case 0x06:
						case 0x07:
							@base = regIdx0(mem8);
							mem8_loc = regs[@base];
							break;
						case 0x08:
						case 0x09:
						case 0x0a:
						case 0x0b:
						case 0x0d:
						case 0x0e:
						case 0x0f:
							mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							@base = regIdx0(mem8);
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							break;
						case 0x10:
						case 0x11:
						case 0x12:
						case 0x13:
						case 0x15:
						case 0x16:
						case 0x17:
						default:
							mem8_loc = phys_mem8_uint();
							@base = regIdx0(mem8);
							mem8_loc = ((mem8_loc + regs[@base]) >> 0);
							break;
					}
					Sb = (int)(CS_flags & 0x000f);
					if (Sb == 0)
					{
						if (@base == 4 || @base == 5)
							Sb = 2;
						else
							Sb = 3;
					}
					else
					{
						Sb--;
					}
					mem8_loc = ((mem8_loc + cpu.segs[Sb].@base) >> 0);
					return mem8_loc;
				}
			}

			private int ld16_mem8_direct()
			{
				int x;
				int y;
				x = phys_mem8[physmem8_ptr++];
				y = phys_mem8[physmem8_ptr++];
				return x | (y << 8);
			}

			/// <summary>
			/// Register Manipulation
			/// </summary>
			internal void set_word_in_register(int reg_idx1, uint x)
			{
				/*
					if arg[0] is = 1xx  then set register xx's upper two bytes to two bytes in arg[1]
					if arg[0] is = 0xx  then set register xx's lower two bytes to two bytes in arg[1]
				*/
				if ((reg_idx1 & 4) != 0)
					regs[reg_idx1 & 3] = (uint)((regs[reg_idx1 & 3] & -65281) | ((x & 0xff) << 8));
				else
					regs[reg_idx1 & 3] = (uint)((regs[reg_idx1 & 3] & -256) | (x & 0xff));
			}

			internal uint do_8bit_math(int conditional_var, uint Yb, uint Zb)
			{
				uint ac = 0;
				switch (conditional_var)
				{
					case 0:
						u_src = Zb;
						Yb = (((Yb + Zb) << 24) >> 24);
						u_dst = Yb;
						_op = 0;
						break;
					case 1:
						Yb = (uint)((int)((Yb | Zb) << 24) >> 24);
						u_dst = Yb;
						_op = 12;
						break;
					case 2:
						ac = check_carry();
						u_src = Zb;
						Yb = (((Yb + Zb + ac) << 24) >> 24);
						u_dst = Yb;
						_op = ac != 0 ? 3 : 0;
						break;
					case 3:
						ac = check_carry();
						u_src = Zb;
						Yb = (((Yb - Zb - ac) << 24) >> 24);
						u_dst = Yb;
						_op = ac != 0 ? 9 : 6;
						break;
					case 4:
						Yb = (((Yb & Zb) << 24) >> 24);
						u_dst = Yb;
						_op = 12;
						break;
					case 5:
						u_src = Zb;
						Yb = (((Yb - Zb) << 24) >> 24);
						u_dst = Yb;
						_op = 6;
						break;
					case 6:
						Yb = (((Yb ^ Zb) << 24) >> 24);
						u_dst = Yb;
						_op = 12;
						break;
					case 7:
						u_src = Zb;
						u_dst = (uint)(((int)(Yb - Zb) << 24) >> 24);
						_op = 6;
						break;
					default:
						throw new Exception("arith" + conditional_var + ": invalid op");
				}
				return Yb;
			}

			/*
			 Paged Memory Mode Access Routines
			 ================================================================================
			*/
			/* Storing XOR values as small lookup table is software equivalent of a Translation Lookaside Buffer (TLB) */

			private byte __ld_8bits_mem8_read()
			{
				do_tlb_set_page(mem8_loc, false, cpu.cpl == 3);
				var tlb_lookup = (uint)(_tlb_read_[mem8_loc >> 12] ^ mem8_loc);
				return phys_mem8[tlb_lookup];
			}

			private uint operation_size_function(uint eipOffset, uint OPbyte)
			{
				int @base;
				int conditional_var;
				int stride;
				var n = 1;
				var CS_flags = init_CS_flags;
				if ((CS_flags & 0x0100) != 0) //are we in 16bit compatibility mode?
					stride = 2;
				else
					stride = 4;
			EXEC_LOOP:
				for (; ; )
				{
					var mem8 = 0;
					uint local_OPbyte_var = 0;
					switch (OPbyte)
					{
						case 0x66: //   Operand-size override prefix
							if ((init_CS_flags & 0x0100) != 0)
							{
								stride = 4;
								CS_flags = (uint)(CS_flags & ~0x0100);
							}
							else
							{
								stride = 2;
								CS_flags |= 0x0100;
							}
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint)((eip_offset + (n++)) >> 0);
								OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							break;
						case 0xf0: //LOCK   Assert LOCK# Signal Prefix
						case 0xf2: //REPNZ  eCX Repeat String Operation Prefix
						case 0xf3: //REPZ  eCX Repeat String Operation Prefix
						case 0x26: //ES ES  ES segment override prefix
						case 0x2e: //CS CS  CS segment override prefix
						case 0x36: //SS SS  SS segment override prefix
						case 0x3e: //DS DS  DS segment override prefix
						case 0x64: //FS FS  FS segment override prefix
						case 0x65: //GS GS  GS segment override prefix
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint)((eip_offset + (n++)) >> 0);
								OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							break;
						case 0x67: //   Address-size override prefix
							if ((init_CS_flags & 0x0080) != 0)
							{
								CS_flags = (uint)(CS_flags & ~0x0080);
							}
							else
							{
								CS_flags |= 0x0080;
							}
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint)((eip_offset + (n++)) >> 0);
								OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							break;
						case 0x91:
						case 0x92:
						case 0x93:
						case 0x94:
						case 0x95:
						case 0x96:
						case 0x97:
						case 0x40: //INC  Zv Increment by 1
						case 0x41: //REX.B   Extension of r/m field, base field, or opcode reg field
						case 0x42: //REX.X   Extension of SIB index field
						case 0x43: //REX.XB   REX.X and REX.B combination
						case 0x44: //REX.R   Extension of ModR/M reg field
						case 0x45: //REX.RB   REX.R and REX.B combination
						case 0x46: //REX.RX   REX.R and REX.X combination
						case 0x47: //REX.RXB   REX.R, REX.X and REX.B combination
						case 0x48: //DEC  Zv Decrement by 1
						case 0x49: //REX.WB   REX.W and REX.B combination
						case 0x4a: //REX.WX   REX.W and REX.X combination
						case 0x4b: //REX.WXB   REX.W, REX.X and REX.B combination
						case 0x4c: //REX.WR   REX.W and REX.R combination
						case 0x4d: //REX.WRB   REX.W, REX.R and REX.B combination
						case 0x4e: //REX.WRX   REX.W, REX.R and REX.X combination
						case 0x4f: //REX.WRXB   REX.W, REX.R, REX.X and REX.B combination
						case 0x50: //PUSH Zv SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x51:
						case 0x52:
						case 0x53:
						case 0x54:
						case 0x55:
						case 0x56:
						case 0x57:
						case 0x58: //POP SS:[rSP] Zv Pop a Value from the Stack
						case 0x59:
						case 0x5a:
						case 0x5b:
						case 0x5c:
						case 0x5d:
						case 0x5e:
						case 0x5f:
						case 0x98: //CBW AL AX Convert Byte to Word
						case 0x99: //CWD AX DX Convert Word to Doubleword
						case 0xc9: //LEAVE SS:[rSP] eBP High Level Procedure Exit
						case 0x9c: //PUSHF Flags SS:[rSP] Push FLAGS Register onto the Stack
						case 0x9d: //POPF SS:[rSP] Flags Pop Stack into FLAGS Register
						case 0x06: //PUSH ES SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x0e: //PUSH CS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x16: //PUSH SS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x1e: //PUSH DS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x07: //POP SS:[rSP] ES Pop a Value from the Stack
						case 0x17: //POP SS:[rSP] SS Pop a Value from the Stack
						case 0x1f: //POP SS:[rSP] DS Pop a Value from the Stack
						case 0xc3: //RETN SS:[rSP]  Return from procedure
						case 0xcb: //RETF SS:[rSP]  Return from procedure
						case 0x90: //XCHG  Zvqp Exchange Register/Memory with Register
						case 0xcc: //INT 3 SS:[rSP] Call to Interrupt Procedure
						case 0xce: //INTO eFlags SS:[rSP] Call to Interrupt Procedure
						case 0xcf: //IRET SS:[rSP] Flags Interrupt Return
						case 0xf5: //CMC   Complement Carry Flag
						case 0xf8: //CLC   Clear Carry Flag
						case 0xf9: //STC   Set Carry Flag
						case 0xfc: //CLD   Clear Direction Flag
						case 0xfd: //STD   Set Direction Flag
						case 0xfa: //CLI   Clear Interrupt Flag
						case 0xfb: //STI   Set Interrupt Flag
						case 0x9e: //SAHF AH  Store AH into Flags
						case 0x9f: //LAHF  AH Load Status Flags into AH Register
						case 0xf4: //HLT   Halt
						case 0xa4: //MOVS (DS:)[rSI] (ES:)[rDI] Move Data from String to String
						case 0xa5: //MOVS DS:[SI] ES:[DI] Move Data from String to String
						case 0xaa: //STOS AL (ES:)[rDI] Store String
						case 0xab: //STOS AX ES:[DI] Store String
						case 0xa6: //CMPS (ES:)[rDI]  Compare String Operands
						case 0xa7: //CMPS ES:[DI]  Compare String Operands
						case 0xac: //LODS (DS:)[rSI] AL Load String
						case 0xad: //LODS DS:[SI] AX Load String
						case 0xae: //SCAS (ES:)[rDI]  Scan String
						case 0xaf: //SCAS ES:[DI]  Scan String
						case 0x9b: //FWAIT   Check pending unmasked floating-point exceptions
						case 0xec: //IN DX AL Input from Port
						case 0xed: //IN DX eAX Input from Port
						case 0xee: //OUT AL DX Output to Port
						case 0xef: //OUT eAX DX Output to Port
						case 0xd7: //XLAT (DS:)[rBX+AL] AL Table Look-up Translation
						case 0x27: //DAA  AL Decimal Adjust AL after Addition
						case 0x2f: //DAS  AL Decimal Adjust AL after Subtraction
						case 0x37: //AAA  AL ASCII Adjust After Addition
						case 0x3f: //AAS  AL ASCII Adjust AL After Subtraction
						case 0x60: //PUSHA AX SS:[rSP] Push All General-Purpose Registers
						case 0x61: //POPA SS:[rSP] DI Pop All General-Purpose Registers
						case 0x6c: //INS DX (ES:)[rDI] Input from Port to String
						case 0x6d: //INS DX ES:[DI] Input from Port to String
						case 0x6e: //OUTS (DS):[rSI] DX Output String to Port
						case 0x6f: //OUTS DS:[SI] DX Output String to Port
							goto EXEC_LOOP_EXIT;
						case 0xb0: //MOV Ib Zb Move
						case 0xb1:
						case 0xb2:
						case 0xb3:
						case 0xb4:
						case 0xb5:
						case 0xb6:
						case 0xb7:
						case 0x04: //ADD Ib AL Add
						case 0x0c: //OR Ib AL Logical Inclusive OR
						case 0x14: //ADC Ib AL Add with Carry
						case 0x1c: //SBB Ib AL Integer Subtraction with Borrow
						case 0x24: //AND Ib AL Logical AND
						case 0x2c: //SUB Ib AL Subtract
						case 0x34: //XOR Ib AL Logical Exclusive OR
						case 0x3c: //CMP AL  Compare Two Operands
						case 0xa8: //TEST AL  Logical Compare
						case 0x6a: //PUSH Ibss SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0xeb: //JMP Jbs  Jump
						case 0x70: //JO Jbs  Jump short if overflow (OF=1)
						case 0x71: //JNO Jbs  Jump short if not overflow (OF=0)
						case 0x72: //JB Jbs  Jump short if below/not above or equal/carry (CF=1)
						case 0x73: //JNB Jbs  Jump short if not below/above or equal/not carry (CF=0)
						case 0x76: //JBE Jbs  Jump short if below or equal/not above (CF=1 AND ZF=1)
						case 0x77: //JNBE Jbs  Jump short if not below or equal/above (CF=0 AND ZF=0)
						case 0x78: //JS Jbs  Jump short if sign (SF=1)
						case 0x79: //JNS Jbs  Jump short if not sign (SF=0)
						case 0x7a: //JP Jbs  Jump short if parity/parity even (PF=1)
						case 0x7b: //JNP Jbs  Jump short if not parity/parity odd
						case 0x7c: //JL Jbs  Jump short if less/not greater (SF!=OF)
						case 0x7d: //JNL Jbs  Jump short if not less/greater or equal (SF=OF)
						case 0x7e: //JLE Jbs  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
						case 0x7f: //JNLE Jbs  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
						case 0x74: //JZ Jbs  Jump short if zero/equal (ZF=0)
						case 0x75: //JNZ Jbs  Jump short if not zero/not equal (ZF=1)
						case 0xe0: //LOOPNZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=0
						case 0xe1: //LOOPZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=1
						case 0xe2: //LOOP Jbs eCX Decrement count; Jump short if count!=0
						case 0xe3: //JCXZ Jbs  Jump short if eCX register is 0
						case 0xcd: //INT Ib SS:[rSP] Call to Interrupt Procedure
						case 0xe4: //IN Ib AL Input from Port
						case 0xe5: //IN Ib eAX Input from Port
						case 0xe6: //OUT AL Ib Output to Port
						case 0xe7: //OUT eAX Ib Output to Port
						case 0xd4: //AAM  AL ASCII Adjust AX After Multiply
						case 0xd5: //AAD  AL ASCII Adjust AX Before Division
							n++;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xb8: //MOV Ivqp Zvqp Move
						case 0xb9:
						case 0xba:
						case 0xbb:
						case 0xbc:
						case 0xbd:
						case 0xbe:
						case 0xbf:
						case 0x05: //ADD Ivds rAX Add
						case 0x0d: //OR Ivds rAX Logical Inclusive OR
						case 0x15: //ADC Ivds rAX Add with Carry
						case 0x1d: //SBB Ivds rAX Integer Subtraction with Borrow
						case 0x25: //AND Ivds rAX Logical AND
						case 0x2d: //SUB Ivds rAX Subtract
						case 0x35: //XOR Ivds rAX Logical Exclusive OR
						case 0x3d: //CMP rAX  Compare Two Operands
						case 0xa9: //TEST rAX  Logical Compare
						case 0x68: //PUSH Ivs SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0xe9: //JMP Jvds  Jump
						case 0xe8: //CALL Jvds SS:[rSP] Call Procedure
							n += stride;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0x88: //MOV Gb Eb Move
						case 0x89: //MOV Gvqp Evqp Move
						case 0x8a: //MOV Eb Gb Move
						case 0x8b: //MOV Evqp Gvqp Move
						case 0x86: //XCHG  Gb Exchange Register/Memory with Register
						case 0x87: //XCHG  Gvqp Exchange Register/Memory with Register
						case 0x8e: //MOV Ew Sw Move
						case 0x8c: //MOV Sw Mw Move
						case 0xc4: //LES Mp ES Load Far Pointer
						case 0xc5: //LDS Mp DS Load Far Pointer
						case 0x00: //ADD Gb Eb Add
						case 0x08: //OR Gb Eb Logical Inclusive OR
						case 0x10: //ADC Gb Eb Add with Carry
						case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
						case 0x20: //AND Gb Eb Logical AND
						case 0x28: //SUB Gb Eb Subtract
						case 0x30: //XOR Gb Eb Logical Exclusive OR
						case 0x38: //CMP Eb  Compare Two Operands
						case 0x01: //ADD Gvqp Evqp Add
						case 0x09: //OR Gvqp Evqp Logical Inclusive OR
						case 0x11: //ADC Gvqp Evqp Add with Carry
						case 0x19: //SBB Gvqp Evqp Integer Subtraction with Borrow
						case 0x21: //AND Gvqp Evqp Logical AND
						case 0x29: //SUB Gvqp Evqp Subtract
						case 0x31: //XOR Gvqp Evqp Logical Exclusive OR
						case 0x39: //CMP Evqp  Compare Two Operands
						case 0x02: //ADD Eb Gb Add
						case 0x0a: //OR Eb Gb Logical Inclusive OR
						case 0x12: //ADC Eb Gb Add with Carry
						case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
						case 0x22: //AND Eb Gb Logical AND
						case 0x2a: //SUB Eb Gb Subtract
						case 0x32: //XOR Eb Gb Logical Exclusive OR
						case 0x3a: //CMP Gb  Compare Two Operands
						case 0x03: //ADD Evqp Gvqp Add
						case 0x0b: //OR Evqp Gvqp Logical Inclusive OR
						case 0x13: //ADC Evqp Gvqp Add with Carry
						case 0x1b: //SBB Evqp Gvqp Integer Subtraction with Borrow
						case 0x23: //AND Evqp Gvqp Logical AND
						case 0x2b: //SUB Evqp Gvqp Subtract
						case 0x33: //XOR Evqp Gvqp Logical Exclusive OR
						case 0x3b: //CMP Gvqp  Compare Two Operands
						case 0x84: //TEST Eb  Logical Compare
						case 0x85: //TEST Evqp  Logical Compare
						case 0xd0: //ROL 1 Eb Rotate
						case 0xd1: //ROL 1 Evqp Rotate
						case 0xd2: //ROL CL Eb Rotate
						case 0xd3: //ROL CL Evqp Rotate
						case 0x8f: //POP SS:[rSP] Ev Pop a Value from the Stack
						case 0x8d: //LEA M Gvqp Load Effective Address
						case 0xfe: //INC  Eb Increment by 1
						case 0xff: //INC  Evqp Increment by 1
						case 0xd8: //FADD Msr ST Add
						case 0xd9: //FLD ESsr ST Load Floating Point Value
						case 0xda: //FIADD Mdi ST Add
						case 0xdb: //FILD Mdi ST Load Integer
						case 0xdc: //FADD Mdr ST Add
						case 0xdd: //FLD Mdr ST Load Floating Point Value
						case 0xde: //FIADD Mwi ST Add
						case 0xdf: //FILD Mwi ST Load Integer
						case 0x62: //BOUND Gv SS:[rSP] Check Array Index Against Bounds
						case 0x63: //ARPL Ew  Adjust RPL Field of Segment Selector
							{
								{
									if ((n + 1) > 15)
										abort(6);
									mem8_loc = (uint)((eip_offset + (n++)) >> 0);
									mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								if ((CS_flags & 0x0080) != 0)
								{
									switch(MOD)
									{
										case 0:
											if ((regIdx0(mem8)) == 6)
												n += 2;
											break;
										case 1:
											n++;
											break;
										default:
											n += 2;
											break;
									}
								}
								else
								{
									switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
									{
										case 0x04:
											{
												if ((n + 1) > 15)
													abort(6);
												mem8_loc = (uint)((eip_offset + (n++)) >> 0);
												local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
													? __ld_8bits_mem8_read()
													: phys_mem8[mem8_loc ^ last_tlb_val]);
											}
											if ((local_OPbyte_var & 7) == 5)
											{
												n += 4;
											}
											break;
										case 0x0c:
											n += 2;
											break;
										case 0x14:
											n += 5;
											break;
										case 0x05:
											n += 4;
											break;
										case 0x00:
										case 0x01:
										case 0x02:
										case 0x03:
										case 0x06:
										case 0x07:
											break;
										case 0x08:
										case 0x09:
										case 0x0a:
										case 0x0b:
										case 0x0d:
										case 0x0e:
										case 0x0f:
											n++;
											break;
										case 0x10:
										case 0x11:
										case 0x12:
										case 0x13:
										case 0x15:
										case 0x16:
										case 0x17:
											n += 4;
											break;
									}
								}
								if (n > 15)
									abort(6);
							}
							goto EXEC_LOOP_EXIT;
						case 0xa0: //MOV Ob AL Move
						case 0xa1: //MOV Ovqp rAX Move
						case 0xa2: //MOV AL Ob Move
						case 0xa3: //MOV rAX Ovqp Move
							if ((CS_flags & 0x0100) != 0)
								n += 2;
							else
								n += 4;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xc6: //MOV Ib Eb Move
						case 0x80: //ADD Ib Eb Add
						case 0x82: //ADD Ib Eb Add
						case 0x83: //ADD Ibs Evqp Add
						case 0x6b: //IMUL Evqp Gvqp Signed Multiply
						case 0xc0: //ROL Ib Eb Rotate
						case 0xc1: //ROL Ib Evqp Rotate
							{
								{
									if ((n + 1) > 15)
										abort(6);
									mem8_loc = (uint)((eip_offset + (n++)) >> 0);
									mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								if ((CS_flags & 0x0080) != 0)
								{
									switch(MOD)
									{
										case 0:
											if ((regIdx0(mem8)) == 6)
												n += 2;
											break;
										case 1:
											n++;
											break;
										default:
											n += 2;
											break;
									}
								}
								else
								{
									switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
									{
										case 0x04:
											{
												if ((n + 1) > 15)
													abort(6);
												mem8_loc = (uint)((eip_offset + (n++)) >> 0);
												local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
													? __ld_8bits_mem8_read()
													: phys_mem8[mem8_loc ^ last_tlb_val]);
											}
											if ((local_OPbyte_var & 7) == 5)
											{
												n += 4;
											}
											break;
										case 0x0c:
											n += 2;
											break;
										case 0x14:
											n += 5;
											break;
										case 0x05:
											n += 4;
											break;
										case 0x00:
										case 0x01:
										case 0x02:
										case 0x03:
										case 0x06:
										case 0x07:
											break;
										case 0x08:
										case 0x09:
										case 0x0a:
										case 0x0b:
										case 0x0d:
										case 0x0e:
										case 0x0f:
											n++;
											break;
										case 0x10:
										case 0x11:
										case 0x12:
										case 0x13:
										case 0x15:
										case 0x16:
										case 0x17:
											n += 4;
											break;
									}
								}
								if (n > 15)
									abort(6);
							}
							n++;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xc7: //MOV Ivds Evqp Move
						case 0x81: //ADD Ivds Evqp Add
						case 0x69: //IMUL Evqp Gvqp Signed Multiply
							{
								{
									if ((n + 1) > 15)
										abort(6);
									mem8_loc = (uint)((eip_offset + (n++)) >> 0);
									mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								if ((CS_flags & 0x0080) != 0)
								{
									switch(MOD)
									{
										case 0:
											if ((regIdx0(mem8)) == 6)
												n += 2;
											break;
										case 1:
											n++;
											break;
										default:
											n += 2;
											break;
									}
								}
								else
								{
									switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
									{
										case 0x04:
											{
												if ((n + 1) > 15)
													abort(6);
												mem8_loc = (uint)((eip_offset + (n++)) >> 0);
												local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
													? __ld_8bits_mem8_read()
													: phys_mem8[mem8_loc ^ last_tlb_val]);
											}
											if ((local_OPbyte_var & 7) == 5)
											{
												n += 4;
											}
											break;
										case 0x0c:
											n += 2;
											break;
										case 0x14:
											n += 5;
											break;
										case 0x05:
											n += 4;
											break;
										case 0x00:
										case 0x01:
										case 0x02:
										case 0x03:
										case 0x06:
										case 0x07:
											break;
										case 0x08:
										case 0x09:
										case 0x0a:
										case 0x0b:
										case 0x0d:
										case 0x0e:
										case 0x0f:
											n++;
											break;
										case 0x10:
										case 0x11:
										case 0x12:
										case 0x13:
										case 0x15:
										case 0x16:
										case 0x17:
											n += 4;
											break;
									}
								}
								if (n > 15)
									abort(6);
							}
							n += stride;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xf6: //TEST Eb  Logical Compare
							{
								{
									if ((n + 1) > 15)
										abort(6);
									mem8_loc = (uint)((eip_offset + (n++)) >> 0);
									mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								if ((CS_flags & 0x0080) != 0)
								{
									switch(MOD)
									{
										case 0:
											if ((regIdx0(mem8)) == 6)
												n += 2;
											break;
										case 1:
											n++;
											break;
										default:
											n += 2;
											break;
									}
								}
								else
								{
									switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
									{
										case 0x04:
											{
												if ((n + 1) > 15)
													abort(6);
												mem8_loc = (uint)((eip_offset + (n++)) >> 0);
												local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
													? __ld_8bits_mem8_read()
													: phys_mem8[mem8_loc ^ last_tlb_val]);
											}
											if ((local_OPbyte_var & 7) == 5)
											{
												n += 4;
											}
											break;
										case 0x0c:
											n += 2;
											break;
										case 0x14:
											n += 5;
											break;
										case 0x05:
											n += 4;
											break;
										case 0x00:
										case 0x01:
										case 0x02:
										case 0x03:
										case 0x06:
										case 0x07:
											break;
										case 0x08:
										case 0x09:
										case 0x0a:
										case 0x0b:
										case 0x0d:
										case 0x0e:
										case 0x0f:
											n++;
											break;
										case 0x10:
										case 0x11:
										case 0x12:
										case 0x13:
										case 0x15:
										case 0x16:
										case 0x17:
											n += 4;
											break;
									}
								}
								if (n > 15)
									abort(6);
							}
							conditional_var = regIdx1(mem8);
							if (conditional_var == 0)
							{
								n++;
								if (n > 15)
									abort(6);
							}
							goto EXEC_LOOP_EXIT;
						case 0xf7: //TEST Evqp  Logical Compare
							{
								{
									if ((n + 1) > 15)
										abort(6);
									mem8_loc = (uint)((eip_offset + (n++)) >> 0);
									mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
										? __ld_8bits_mem8_read()
										: phys_mem8[mem8_loc ^ last_tlb_val]);
								}
								if ((CS_flags & 0x0080) != 0)
								{
									switch(MOD)
									{
										case 0:
											if ((regIdx0(mem8)) == 6)
												n += 2;
											break;
										case 1:
											n++;
											break;
										default:
											n += 2;
											break;
									}
								}
								else
								{
									switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
									{
										case 0x04:
											{
												if ((n + 1) > 15)
													abort(6);
												mem8_loc = (uint)((eip_offset + (n++)) >> 0);
												local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
													? __ld_8bits_mem8_read()
													: phys_mem8[mem8_loc ^ last_tlb_val]);
											}
											if ((local_OPbyte_var & 7) == 5)
											{
												n += 4;
											}
											break;
										case 0x0c:
											n += 2;
											break;
										case 0x14:
											n += 5;
											break;
										case 0x05:
											n += 4;
											break;
										case 0x00:
										case 0x01:
										case 0x02:
										case 0x03:
										case 0x06:
										case 0x07:
											break;
										case 0x08:
										case 0x09:
										case 0x0a:
										case 0x0b:
										case 0x0d:
										case 0x0e:
										case 0x0f:
											n++;
											break;
										case 0x10:
										case 0x11:
										case 0x12:
										case 0x13:
										case 0x15:
										case 0x16:
										case 0x17:
											n += 4;
											break;
									}
								}
								if (n > 15)
									abort(6);
							}
							conditional_var = regIdx1(mem8);
							if (conditional_var == 0)
							{
								n += stride;
								if (n > 15)
									abort(6);
							}
							goto EXEC_LOOP_EXIT;
						case 0xea: //JMPF Ap  Jump
						case 0x9a: //CALLF Ap SS:[rSP] Call Procedure
							n += 2 + stride;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xc2: //RETN SS:[rSP]  Return from procedure
						case 0xca: //RETF Iw  Return from procedure
							n += 2;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xc8: //ENTER Iw SS:[rSP] Make Stack Frame for Procedure Parameters
							n += 3;
							if (n > 15)
								abort(6);
							goto EXEC_LOOP_EXIT;
						case 0xd6: //SALC   Undefined and Reserved; Does not Generate #UD
						case 0xf1: //INT1   Undefined and Reserved; Does not Generate #UD
						default:
							abort(6);
							if (operation_size_function_2_byte(stride, CS_flags, ref n, ref mem8, ref local_OPbyte_var))
								goto EXEC_LOOP_EXIT;
							break;
						case 0x0f: //two-op instruction prefix
							if (operation_size_function_2_byte(stride, CS_flags, ref n, ref mem8, ref local_OPbyte_var))
								goto EXEC_LOOP_EXIT;
							break;
					}
				}
			EXEC_LOOP_EXIT:
				{
				}

				return (uint)n;
			}

			private bool operation_size_function_2_byte(int stride, uint CS_flags, ref int n, ref int mem8,
				ref uint local_OPbyte_var)
			{
				uint OPbyte;
				{
					if ((n + 1) > 15)
						abort(6);
					mem8_loc = (uint)((eip_offset + (n++)) >> 0);
					OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
						? __ld_8bits_mem8_read()
						: phys_mem8[mem8_loc ^ last_tlb_val]);
				}
				switch (OPbyte)
				{
					case 0x06: //CLTS  CR0 Clear Task-Switched Flag in CR0
					case 0xa2: //CPUID  IA32_BIOS_SIGN_ID CPU Identification
					case 0x31: //RDTSC IA32_TIME_STAMP_COUNTER EAX Read Time-Stamp Counter
					case 0xa0: //PUSH FS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
					case 0xa8: //PUSH GS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
					case 0xa1: //POP SS:[rSP] FS Pop a Value from the Stack
					case 0xa9: //POP SS:[rSP] GS Pop a Value from the Stack
					case 0xc8: //BSWAP  Zvqp Byte Swap
					case 0xc9:
					case 0xca:
					case 0xcb:
					case 0xcc:
					case 0xcd:
					case 0xce:
					case 0xcf:
						return true;
					case 0x80: //JO Jvds  Jump short if overflow (OF=1)
					case 0x81: //JNO Jvds  Jump short if not overflow (OF=0)
					case 0x82: //JB Jvds  Jump short if below/not above or equal/carry (CF=1)
					case 0x83: //JNB Jvds  Jump short if not below/above or equal/not carry (CF=0)
					case 0x84: //JZ Jvds  Jump short if zero/equal (ZF=0)
					case 0x85: //JNZ Jvds  Jump short if not zero/not equal (ZF=1)
					case 0x86: //JBE Jvds  Jump short if below or equal/not above (CF=1 AND ZF=1)
					case 0x87: //JNBE Jvds  Jump short if not below or equal/above (CF=0 AND ZF=0)
					case 0x88: //JS Jvds  Jump short if sign (SF=1)
					case 0x89: //JNS Jvds  Jump short if not sign (SF=0)
					case 0x8a: //JP Jvds  Jump short if parity/parity even (PF=1)
					case 0x8b: //JNP Jvds  Jump short if not parity/parity odd
					case 0x8c: //JL Jvds  Jump short if less/not greater (SF!=OF)
					case 0x8d: //JNL Jvds  Jump short if not less/greater or equal (SF=OF)
					case 0x8e: //JLE Jvds  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
					case 0x8f: //JNLE Jvds  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
						n += stride;
						if (n > 15)
							abort(6);
						return true;
					case 0x90: //SETO  Eb Set Byte on Condition - overflow (OF=1)
					case 0x91: //SETNO  Eb Set Byte on Condition - not overflow (OF=0)
					case 0x92: //SETB  Eb Set Byte on Condition - below/not above or equal/carry (CF=1)
					case 0x93: //SETNB  Eb Set Byte on Condition - not below/above or equal/not carry (CF=0)
					case 0x94: //SETZ  Eb Set Byte on Condition - zero/equal (ZF=0)
					case 0x95: //SETNZ  Eb Set Byte on Condition - not zero/not equal (ZF=1)
					case 0x96: //SETBE  Eb Set Byte on Condition - below or equal/not above (CF=1 AND ZF=1)
					case 0x97: //SETNBE  Eb Set Byte on Condition - not below or equal/above (CF=0 AND ZF=0)
					case 0x98: //SETS  Eb Set Byte on Condition - sign (SF=1)
					case 0x99: //SETNS  Eb Set Byte on Condition - not sign (SF=0)
					case 0x9a: //SETP  Eb Set Byte on Condition - parity/parity even (PF=1)
					case 0x9b: //SETNP  Eb Set Byte on Condition - not parity/parity odd
					case 0x9c: //SETL  Eb Set Byte on Condition - less/not greater (SF!=OF)
					case 0x9d: //SETNL  Eb Set Byte on Condition - not less/greater or equal (SF=OF)
					case 0x9e: //SETLE  Eb Set Byte on Condition - less or equal/not greater ((ZF=1) OR (SF!=OF))
					case 0x9f: //SETNLE  Eb Set Byte on Condition - not less nor equal/greater ((ZF=0) AND (SF=OF))
					case 0x40: //CMOVO Evqp Gvqp Conditional Move - overflow (OF=1)
					case 0x41: //CMOVNO Evqp Gvqp Conditional Move - not overflow (OF=0)
					case 0x42: //CMOVB Evqp Gvqp Conditional Move - below/not above or equal/carry (CF=1)
					case 0x43: //CMOVNB Evqp Gvqp Conditional Move - not below/above or equal/not carry (CF=0)
					case 0x44: //CMOVZ Evqp Gvqp Conditional Move - zero/equal (ZF=0)
					case 0x45: //CMOVNZ Evqp Gvqp Conditional Move - not zero/not equal (ZF=1)
					case 0x46: //CMOVBE Evqp Gvqp Conditional Move - below or equal/not above (CF=1 AND ZF=1)
					case 0x47: //CMOVNBE Evqp Gvqp Conditional Move - not below or equal/above (CF=0 AND ZF=0)
					case 0x48: //CMOVS Evqp Gvqp Conditional Move - sign (SF=1)
					case 0x49: //CMOVNS Evqp Gvqp Conditional Move - not sign (SF=0)
					case 0x4a: //CMOVP Evqp Gvqp Conditional Move - parity/parity even (PF=1)
					case 0x4b: //CMOVNP Evqp Gvqp Conditional Move - not parity/parity odd
					case 0x4c: //CMOVL Evqp Gvqp Conditional Move - less/not greater (SF!=OF)
					case 0x4d: //CMOVNL Evqp Gvqp Conditional Move - not less/greater or equal (SF=OF)
					case 0x4e: //CMOVLE Evqp Gvqp Conditional Move - less or equal/not greater ((ZF=1) OR (SF!=OF))
					case 0x4f: //CMOVNLE Evqp Gvqp Conditional Move - not less nor equal/greater ((ZF=0) AND (SF=OF))
					case 0xb6: //MOVZX Eb Gvqp Move with Zero-Extend
					case 0xb7: //MOVZX Ew Gvqp Move with Zero-Extend
					case 0xbe: //MOVSX Eb Gvqp Move with Sign-Extension
					case 0xbf: //MOVSX Ew Gvqp Move with Sign-Extension
					case 0x00: //SLDT LDTR Mw Store Local Descriptor Table Register
					case 0x01: //SGDT GDTR Ms Store Global Descriptor Table Register
					case 0x02: //LAR Mw Gvqp Load Access Rights Byte
					case 0x03: //LSL Mw Gvqp Load Segment Limit
					case 0x20: //MOV Cd Rd Move to/from Control Registers
					case 0x22: //MOV Rd Cd Move to/from Control Registers
					case 0x23: //MOV Rd Dd Move to/from Debug Registers
					case 0xb2: //LSS Mptp SS Load Far Pointer
					case 0xb4: //LFS Mptp FS Load Far Pointer
					case 0xb5: //LGS Mptp GS Load Far Pointer
					case 0xa5: //SHLD Gvqp Evqp Double Precision Shift Left
					case 0xad: //SHRD Gvqp Evqp Double Precision Shift Right
					case 0xa3: //BT Evqp  Bit Test
					case 0xab: //BTS Gvqp Evqp Bit Test and Set
					case 0xb3: //BTR Gvqp Evqp Bit Test and Reset
					case 0xbb: //BTC Gvqp Evqp Bit Test and Complement
					case 0xbc: //BSF Evqp Gvqp Bit Scan Forward
					case 0xbd: //BSR Evqp Gvqp Bit Scan Reverse
					case 0xaf: //IMUL Evqp Gvqp Signed Multiply
					case 0xc0: //XADD  Eb Exchange and Add
					case 0xc1: //XADD  Evqp Exchange and Add
					case 0xb0: //CMPXCHG Gb Eb Compare and Exchange
					case 0xb1: //CMPXCHG Gvqp Evqp Compare and Exchange
						{
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint)((eip_offset + (n++)) >> 0);
								mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							if ((CS_flags & 0x0080) != 0)
							{
								switch(MOD)
								{
									case 0:
										if ((regIdx0(mem8)) == 6)
											n += 2;
										break;
									case 1:
										n++;
										break;
									default:
										n += 2;
										break;
								}
							}
							else
							{
								switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
								{
									case 0x04:
										{
											if ((n + 1) > 15)
												abort(6);
											mem8_loc = (uint)((eip_offset + (n++)) >> 0);
											local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
												? __ld_8bits_mem8_read()
												: phys_mem8[mem8_loc ^ last_tlb_val]);
										}
										if ((local_OPbyte_var & 7) == 5)
										{
											n += 4;
										}
										break;
									case 0x0c:
										n += 2;
										break;
									case 0x14:
										n += 5;
										break;
									case 0x05:
										n += 4;
										break;
									case 0x00:
									case 0x01:
									case 0x02:
									case 0x03:
									case 0x06:
									case 0x07:
										break;
									case 0x08:
									case 0x09:
									case 0x0a:
									case 0x0b:
									case 0x0d:
									case 0x0e:
									case 0x0f:
										n++;
										break;
									case 0x10:
									case 0x11:
									case 0x12:
									case 0x13:
									case 0x15:
									case 0x16:
									case 0x17:
										n += 4;
										break;
								}
							}
							if (n > 15)
								abort(6);
						}
						return true;
					case 0xa4: //SHLD Gvqp Evqp Double Precision Shift Left
					case 0xac: //SHRD Gvqp Evqp Double Precision Shift Right
					case 0xba: //BT Evqp  Bit Test
						{
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint)((eip_offset + (n++)) >> 0);
								mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							if ((CS_flags & 0x0080) != 0)
							{
								switch(MOD)
								{
									case 0:
										if ((regIdx0(mem8)) == 6)
											n += 2;
										break;
									case 1:
										n++;
										break;
									default:
										n += 2;
										break;
								}
							}
							else
							{
								switch ((regIdx0(mem8)) | ((mem8 >> 3) & 0x18))
								{
									case 0x04:
										{
											if ((n + 1) > 15)
												abort(6);
											mem8_loc = (uint)((eip_offset + (n++)) >> 0);
											local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
												? __ld_8bits_mem8_read()
												: phys_mem8[mem8_loc ^ last_tlb_val]);
										}
										if ((local_OPbyte_var & 7) == 5)
										{
											n += 4;
										}
										break;
									case 0x0c:
										n += 2;
										break;
									case 0x14:
										n += 5;
										break;
									case 0x05:
										n += 4;
										break;
									case 0x00:
									case 0x01:
									case 0x02:
									case 0x03:
									case 0x06:
									case 0x07:
										break;
									case 0x08:
									case 0x09:
									case 0x0a:
									case 0x0b:
									case 0x0d:
									case 0x0e:
									case 0x0f:
										n++;
										break;
									case 0x10:
									case 0x11:
									case 0x12:
									case 0x13:
									case 0x15:
									case 0x16:
									case 0x17:
										n += 4;
										break;
								}
							}
							if (n > 15)
								abort(6);
						}
						n++;
						if (n > 15)
							abort(6);
						return true;
					case 0x04:
					case 0x05: //LOADALL  AX Load All of the CPU Registers
					case 0x07: //LOADALL  EAX Load All of the CPU Registers
					case 0x08: //INVD   Invalidate Internal Caches
					case 0x09: //WBINVD   Write Back and Invalidate Cache
					case 0x0a:
					case 0x0b: //UD2   Undefined Instruction
					case 0x0c:
					case 0x0d: //NOP Ev  No Operation
					case 0x0e:
					case 0x0f:
					case 0x10: //MOVUPS Wps Vps Move Unaligned Packed Single-FP Values
					case 0x11: //MOVUPS Vps Wps Move Unaligned Packed Single-FP Values
					case 0x12: //MOVHLPS Uq Vq Move Packed Single-FP Values High to Low
					case 0x13: //MOVLPS Vq Mq Move Low Packed Single-FP Values
					case 0x14: //UNPCKLPS Wq Vps Unpack and Interleave Low Packed Single-FP Values
					case 0x15: //UNPCKHPS Wq Vps Unpack and Interleave High Packed Single-FP Values
					case 0x16: //MOVLHPS Uq Vq Move Packed Single-FP Values Low to High
					case 0x17: //MOVHPS Vq Mq Move High Packed Single-FP Values
					case 0x18: //HINT_NOP Ev  Hintable NOP
					case 0x19: //HINT_NOP Ev  Hintable NOP
					case 0x1a: //HINT_NOP Ev  Hintable NOP
					case 0x1b: //HINT_NOP Ev  Hintable NOP
					case 0x1c: //HINT_NOP Ev  Hintable NOP
					case 0x1d: //HINT_NOP Ev  Hintable NOP
					case 0x1e: //HINT_NOP Ev  Hintable NOP
					case 0x1f: //HINT_NOP Ev  Hintable NOP
					case 0x21: //MOV Dd Rd Move to/from Debug Registers
					case 0x24: //MOV Td Rd Move to/from Test Registers
					case 0x25:
					case 0x26: //MOV Rd Td Move to/from Test Registers
					case 0x27:
					case 0x28: //MOVAPS Wps Vps Move Aligned Packed Single-FP Values
					case 0x29: //MOVAPS Vps Wps Move Aligned Packed Single-FP Values
					case 0x2a: //CVTPI2PS Qpi Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
					case 0x2b: //MOVNTPS Vps Mps Store Packed Single-FP Values Using Non-Temporal Hint
					case 0x2c: //CVTTPS2PI Wpsq Ppi Convert with Trunc. Packed Single-FP Values to1.11 PackedDW Integers
					case 0x2d: //CVTPS2PI Wpsq Ppi Convert Packed Single-FP Values to1.11 PackedDW Integers
					case 0x2e: //UCOMISS Vss  Unordered Compare Scalar Single-FP Values and Set EFLAGS
					case 0x2f: //COMISS Vss  Compare Scalar Ordered Single-FP Values and Set EFLAGS
					case 0x30: //WRMSR rCX MSR Write to Model Specific Register
					case 0x32: //RDMSR rCX rAX Read from Model Specific Register
					case 0x33: //RDPMC PMC EAX Read Performance-Monitoring Counters
					case 0x34: //SYSENTER IA32_SYSENTER_CS SS Fast System Call
					case 0x35: //SYSEXIT IA32_SYSENTER_CS SS Fast Return from Fast System Call
					case 0x36:
					case 0x37: //GETSEC EAX  GETSEC Leaf Functions
					case 0x38: //PSHUFB Qq Pq Packed Shuffle Bytes
					case 0x39:
					case 0x3a: //ROUNDPS Wps Vps Round Packed Single-FP Values
					case 0x3b:
					case 0x3c:
					case 0x3d:
					case 0x3e:
					case 0x3f:
					case 0x50: //MOVMSKPS Ups Gdqp Extract Packed Single-FP Sign Mask
					case 0x51: //SQRTPS Wps Vps Compute Square Roots of Packed Single-FP Values
					case 0x52: //RSQRTPS Wps Vps Compute Recipr. of Square Roots of Packed Single-FP Values
					case 0x53: //RCPPS Wps Vps Compute Reciprocals of Packed Single-FP Values
					case 0x54: //ANDPS Wps Vps Bitwise Logical AND of Packed Single-FP Values
					case 0x55: //ANDNPS Wps Vps Bitwise Logical AND NOT of Packed Single-FP Values
					case 0x56: //ORPS Wps Vps Bitwise Logical OR of Single-FP Values
					case 0x57: //XORPS Wps Vps Bitwise Logical XOR for Single-FP Values
					case 0x58: //ADDPS Wps Vps Add Packed Single-FP Values
					case 0x59: //MULPS Wps Vps Multiply Packed Single-FP Values
					case 0x5a: //CVTPS2PD Wps Vpd Convert Packed Single-FP Values to1.11 PackedDouble-FP Values
					case 0x5b: //CVTDQ2PS Wdq Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
					case 0x5c: //SUBPS Wps Vps Subtract Packed Single-FP Values
					case 0x5d: //MINPS Wps Vps Return Minimum Packed Single-FP Values
					case 0x5e: //DIVPS Wps Vps Divide Packed Single-FP Values
					case 0x5f: //MAXPS Wps Vps Return Maximum Packed Single-FP Values
					case 0x60: //PUNPCKLBW Qd Pq Unpack Low Data
					case 0x61: //PUNPCKLWD Qd Pq Unpack Low Data
					case 0x62: //PUNPCKLDQ Qd Pq Unpack Low Data
					case 0x63: //PACKSSWB Qd Pq Pack with Signed Saturation
					case 0x64: //PCMPGTB Qd Pq Compare Packed Signed Integers for Greater Than
					case 0x65: //PCMPGTW Qd Pq Compare Packed Signed Integers for Greater Than
					case 0x66: //PCMPGTD Qd Pq Compare Packed Signed Integers for Greater Than
					case 0x67: //PACKUSWB Qq Pq Pack with Unsigned Saturation
					case 0x68: //PUNPCKHBW Qq Pq Unpack High Data
					case 0x69: //PUNPCKHWD Qq Pq Unpack High Data
					case 0x6a: //PUNPCKHDQ Qq Pq Unpack High Data
					case 0x6b: //PACKSSDW Qq Pq Pack with Signed Saturation
					case 0x6c: //PUNPCKLQDQ Wdq Vdq Unpack Low Data
					case 0x6d: //PUNPCKHQDQ Wdq Vdq Unpack High Data
					case 0x6e: //MOVD Ed Pq Move Doubleword
					case 0x6f: //MOVQ Qq Pq Move Quadword
					case 0x70: //PSHUFW Qq Pq Shuffle Packed Words
					case 0x71: //PSRLW Ib Nq Shift Packed Data Right Logical
					case 0x72: //PSRLD Ib Nq Shift Double Quadword Right Logical
					case 0x73: //PSRLQ Ib Nq Shift Packed Data Right Logical
					case 0x74: //PCMPEQB Qq Pq Compare Packed Data for Equal
					case 0x75: //PCMPEQW Qq Pq Compare Packed Data for Equal
					case 0x76: //PCMPEQD Qq Pq Compare Packed Data for Equal
					case 0x77: //EMMS   Empty MMX Technology State
					case 0x78: //VMREAD Gd Ed Read Field from Virtual-Machine Control Structure
					case 0x79: //VMWRITE Gd  Write Field to Virtual-Machine Control Structure
					case 0x7a:
					case 0x7b:
					case 0x7c: //HADDPD Wpd Vpd Packed Double-FP Horizontal Add
					case 0x7d: //HSUBPD Wpd Vpd Packed Double-FP Horizontal Subtract
					case 0x7e: //MOVD Pq Ed Move Doubleword
					case 0x7f: //MOVQ Pq Qq Move Quadword
					case 0xa6:
					case 0xa7:
					case 0xaa: //RSM  Flags Resume from System Management Mode
					case 0xae: //FXSAVE ST Mstx Save x87 FPU, MMX, XMM, and MXCSR State
					case 0xb8: //JMPE   Jump to IA-64 Instruction Set
					case 0xb9: //UD G  Undefined Instruction
					case 0xc2: //CMPPS Wps Vps Compare Packed Single-FP Values
					case 0xc3: //MOVNTI Gdqp Mdqp Store Doubleword Using Non-Temporal Hint
					case 0xc4: //PINSRW Rdqp Pq Insert Word
					case 0xc5: //PEXTRW Nq Gdqp Extract Word
					case 0xc6: //SHUFPS Wps Vps Shuffle Packed Single-FP Values
					case 0xc7: //CMPXCHG8B EBX Mq Compare and Exchange Bytes
					default:
						abort(6);
						break;
				}
				return true;
			}

			/// <summary>
			/// Typically, the upper 20 bits of CR3 become the page directory base register (PDBR),
			/// which stores the physical address of the first page directory entry.
			/// </summary>
			private void do_tlb_set_page(uint Gd, bool Hd, bool ja)
			{
				int error_code;
				bool Od;
				if ((cpu.cr0 & (1 << 31)) == 0)
				{
					//CR0: bit31 PG Paging If 1, enable paging and use the CR3 register, else disable paging
					cpu.tlb_set_page((int)(Gd & -4096), (int)(Gd & -4096), true);
				}
				else
				{
					var Id = (uint)((cpu.cr3 & -4096) + ((Gd >> 20) & 0xffc));
					var Jd = cpu.ld32_phys(Id);
					if ((Jd & 0x00000001) == 0)
					{
						error_code = 0;
					}
					else
					{
						if ((Jd & 0x00000020) == 0)
						{
							Jd |= 0x00000020;
							cpu.st32_phys(Id, Jd);
						}
						var Kd = (uint)((Jd & -4096) + ((Gd >> 10) & 0xffc));
						var Ld = cpu.ld32_phys(Kd);
						if ((Ld & 0x00000001) == 0)
						{
							error_code = 0;
						}
						else
						{
							var Md = Ld & Jd;
							if (ja && (Md & 0x00000004) == 0)
							{
								error_code = 0x01;
							}
							else if (Hd && (Md & 0x00000002) == 0)
							{
								error_code = 0x01;
							}
							else
							{
								var Nd = (Hd && (Ld & 0x00000040) == 0);
								if ((Ld & 0x00000020) == 0 || Nd)
								{
									Ld |= 0x00000020;
									if (Nd)
										Ld |= 0x00000040;
									cpu.st32_phys(Kd, Ld);
								}
								var ud = false;
								if ((Ld & 0x00000040) != 0 && (Md & 0x00000002) != 0)
									ud = true;
								Od = false;
								if ((Md & 0x00000004) != 0)
									Od = true;
								cpu.tlb_set_page((int)(Gd & -4096), Ld & -4096, ud, Od);
								return;
							}
						}
					}
					error_code |= (Hd ? 1 : 0) << 1;
					if (ja)
						error_code |= 0x04;
					cpu.cr2 = (int)Gd;
					abort_with_error_code(14, error_code);
				}
			}

			/* Oh No You Didn't!
		   Identifier   Description
		   0            Divide error
		   1            Debug exceptions
		   2            Nonmaskable interrupt
		   3            Breakpoint (one-byte INT 3 instruction)
		   4            Overflow (INTO instruction)
		   5            Bounds check (BOUND instruction)
		   6            Invalid opcode
		   7            Coprocessor not available
		   8            Double fault
		   9            (reserved)
		   10           Invalid TSS
		   11           Segment not present
		   12           Stack exception
		   13           General protection
		   14           Page fault
		   15           (reserved)
		   16           Coprecessor error
		   17-31        (reserved)
		   32-255       Available for external interrupts via INTR pin

		   The identifiers of the maskable interrupts are determined by external
		   interrupt controllers (such as Intel's 8259A Programmable Interrupt
		   Controller) and communicated to the processor during the processor's
		   interrupt-acknowledge sequence. The numbers assigned by an 8259A PIC
		   can be specified by software. Any numbers in the range 32 through 255
		   can be used. Table 9-1 shows the assignment of interrupt and exception
		   identifiers.
		 */
			private void abort_with_error_code(int intno, int error_code)
			{
				cpu.cycle_count += (N_cycles - cycles_left);
				cpu.eip = eip;
				cpu.cc_src = (int)u_src;
				cpu.cc_dst = (int)u_dst;
				cpu.cc_op = _op;
				cpu.cc_op2 = _op2;
				cpu.cc_dst2 = _dst2;
				throw new IntNoException() { intno = intno, error_code = error_code };
			}

			private void do_interrupt(int intno, int ne, int error_code, int oe, int pe)
			{
				int qe;
				int he;
				int ue;
				var ke = 0;
				int le;
				int we = 0;
				int xe = 0;
				int ye;
				int SS_mask;
				var te = 0;
				if (ne == 0 && pe == 0)
				{
					switch (intno)
					{
						case 8:
						case 10:
						case 11:
						case 12:
						case 13:
						case 14:
						case 17:
							te = 1;
							break;
					}
				}
				if (ne != 0)
					ye = oe;
				else
					ye = (int)eip;
				var descriptor_table = cpu.idt;
				if (intno * 8 + 7 > descriptor_table.limit)
					abort_with_error_code(13, intno * 8 + 2);
				mem8_loc = (uint)((descriptor_table.@base + intno * 8) & -1);
				var descriptor_low4bytes = ld32_mem8_kernel_read();
				mem8_loc += 4;
				var descriptor_high4bytes = ld32_mem8_kernel_read();
				var descriptor_type = (descriptor_high4bytes >> 8) & 0x1f;
				switch (descriptor_type)
				{
					case 5:
					case 7:
					case 6:
						throw new Exception("unsupported task gate");
					case 14:
					case 15:
						break;
					default:
						abort_with_error_code(13, intno * 8 + 2);
						break;
				}
				var dpl = (descriptor_high4bytes >> 13) & 3;
				var cpl_var = cpu.cpl;
				if (ne != 0 && dpl < cpl_var)
					abort_with_error_code(13, intno * 8 + 2);
				if ((descriptor_high4bytes & (1 << 15)) == 0)
					abort_with_error_code(11, intno * 8 + 2);
				var selector = descriptor_low4bytes >> 16;

				var ve = (descriptor_high4bytes & -65536) | (descriptor_low4bytes & 0x0000ffff);
				if ((selector & 0xfffc) == 0)
					abort_with_error_code(13, 0);
				var e = load_from_descriptor_table((uint)selector);
				if (e == null)
					abort_with_error_code(13, selector & 0xfffc);
				descriptor_low4bytes = e[0];
				descriptor_high4bytes = e[1];
				if ((descriptor_high4bytes & (1 << 12)) == 0 || (descriptor_high4bytes & ((1 << 11))) == 0)
					abort_with_error_code(13, selector & 0xfffc);
				dpl = (descriptor_high4bytes >> 13) & 3;
				if (dpl > cpl_var)
					abort_with_error_code(13, selector & 0xfffc);
				if ((descriptor_high4bytes & (1 << 15)) == 0)
					abort_with_error_code(11, selector & 0xfffc);
				if ((descriptor_high4bytes & (1 << 10)) == 0 && dpl < cpl_var)
				{
					e = load_from_TR(dpl);
					ke = e[0];
					le = e[1];
					if ((ke & 0xfffc) == 0)
						abort_with_error_code(10, ke & 0xfffc);
					if ((ke & 3) != dpl)
						abort_with_error_code(10, ke & 0xfffc);
					e = load_from_descriptor_table((uint)ke);
					if (e == null)
						abort_with_error_code(10, ke & 0xfffc);
					we = e[0];
					xe = e[1];
					var re = (xe >> 13) & 3;
					if (re != dpl)
						abort_with_error_code(10, ke & 0xfffc);
					if ((xe & (1 << 12)) == 0 || (xe & (1 << 11)) != 0 || (xe & (1 << 9)) == 0)
						abort_with_error_code(10, ke & 0xfffc);
					if ((xe & (1 << 15)) == 0)
						abort_with_error_code(10, ke & 0xfffc);
					ue = 1;
					SS_mask = SS_mask_from_flags(xe);
					qe = calculate_descriptor_base(we, xe);
				}
				else if ((descriptor_high4bytes & (1 << 10)) != 0 || dpl == cpl_var)
				{
					if ((cpu.eflags & 0x00020000) != 0)
						abort_with_error_code(13, selector & 0xfffc);
					ue = 0;
					SS_mask = SS_mask_from_flags(cpu.segs[2].flags);
					qe = (int)cpu.segs[2].@base;
					le = (int)regs[4];
					dpl = cpl_var;
				}
				else
				{
					abort_with_error_code(13, selector & 0xfffc);
					ue = 0;
					SS_mask = 0;
					qe = 0;
					le = 0;
				}
				var is_32_bit = descriptor_type >> 3;
				if (is_32_bit == 1)
				{
					if (ue != 0)
					{
						if ((cpu.eflags & 0x00020000) != 0)
						{
							{
								le = (le - 4) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st32_mem8_kernel_write(cpu.segs[5].selector);
							}
							{
								le = (le - 4) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st32_mem8_kernel_write(cpu.segs[4].selector);
							}
							{
								le = (le - 4) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st32_mem8_kernel_write(cpu.segs[3].selector);
							}
							{
								le = (le - 4) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st32_mem8_kernel_write(cpu.segs[0].selector);
							}
						}
						{
							le = (le - 4) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st32_mem8_kernel_write(cpu.segs[2].selector);
						}
						{
							le = (le - 4) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st32_mem8_kernel_write((int)regs[4]);
						}
					}
					{
						le = (le - 4) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st32_mem8_kernel_write((int)get_FLAGS());
					}
					{
						le = (le - 4) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st32_mem8_kernel_write(cpu.segs[1].selector);
					}
					{
						le = (le - 4) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st32_mem8_kernel_write(ye);
					}
					if (te != 0)
					{
						{
							le = (le - 4) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st32_mem8_kernel_write(error_code);
						}
					}
				}
				else
				{
					if (ue != 0)
					{
						if ((cpu.eflags & 0x00020000) != 0)
						{
							{
								le = (le - 2) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st16_mem8_kernel_write(cpu.segs[5].selector);
							}
							{
								le = (le - 2) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st16_mem8_kernel_write(cpu.segs[4].selector);
							}
							{
								le = (le - 2) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st16_mem8_kernel_write(cpu.segs[3].selector);
							}
							{
								le = (le - 2) & -1;
								mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
								st16_mem8_kernel_write(cpu.segs[0].selector);
							}
						}
						{
							le = (le - 2) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st16_mem8_kernel_write(cpu.segs[2].selector);
						}
						{
							le = (le - 2) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st16_mem8_kernel_write((int)regs[4]);
						}
					}
					{
						le = (le - 2) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st16_mem8_kernel_write((int)get_FLAGS());
					}
					{
						le = (le - 2) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st16_mem8_kernel_write(cpu.segs[1].selector);
					}
					{
						le = (le - 2) & -1;
						mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
						st16_mem8_kernel_write(ye);
					}
					if (te != 0)
					{
						{
							le = (le - 2) & -1;
							mem8_loc = (uint)((qe + (le & SS_mask)) & -1);
							st16_mem8_kernel_write(error_code);
						}
					}
				}
				if (ue != 0)
				{
					if ((cpu.eflags & 0x00020000) != 0)
					{
						set_segment_vars(0, 0, 0, 0, 0);
						set_segment_vars(3, 0, 0, 0, 0);
						set_segment_vars(4, 0, 0, 0, 0);
						set_segment_vars(5, 0, 0, 0, 0);
					}
					ke = (ke & ~3) | dpl;
					set_segment_vars(2, ke, (uint)qe, calculate_descriptor_limit(we, xe), xe);
				}
				regs[4] = (uint)((regs[4] & ~SS_mask) | ((le) & SS_mask));
				selector = (selector & ~3) | dpl;
				set_segment_vars(1, selector, (uint)calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes),
					calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes), descriptor_high4bytes);
				change_permission_level(dpl);
				eip = (uint)ve;
				physmem8_ptr = initial_mem_ptr = 0;
				if ((descriptor_type & 1) == 0)
				{
					cpu.eflags &= ~0x00000200;
				}
				cpu.eflags &= ~(0x00000100 | 0x00020000 | 0x00010000 | 0x00004000);
			}

			private void change_permission_level(int sd)
			{
				cpu.cpl = sd;
				if (cpu.cpl == 3)
				{
					_tlb_read_ = tlb_read_user;
					_tlb_write_ = tlb_write_user;
				}
				else
				{
					_tlb_read_ = tlb_read_kernel;
					_tlb_write_ = tlb_write_kernel;
				}
			}

			private void st16_mem8_kernel_write(int selector)
			{
				throw new NotImplementedException();
			}

			/* Segment / Descriptor Handling Functions */
			private int SS_mask_from_flags(int descriptor_high4bytes)
			{
				if ((descriptor_high4bytes & (1 << 22)) != 0)
					return -1;
				else
					return 0xffff;
			}

			private int[] load_from_TR(int he)
			{
				int le;
				if ((cpu.tr.flags & (1 << 15)) == 0)
					cpu_abort("invalid tss"); //task state segment
				var tr_type = (cpu.tr.flags >> 8) & 0xf;
				if ((tr_type & 7) != 1)
					cpu_abort("invalid tss type");
				var is_32_bit = tr_type >> 3;
				var Rb = (he * 4 + 2) << is_32_bit;
				if (Rb + (4 << is_32_bit) - 1 > cpu.tr.limit)
					abort_with_error_code(10, cpu.tr.selector & 0xfffc);
				mem8_loc = (uint)((cpu.tr.@base + Rb) & -1);
				if (is_32_bit == 0)
				{
					le = ld16_mem8_kernel_read();
					mem8_loc += 2;
				}
				else
				{
					le = ld32_mem8_kernel_read();
					mem8_loc += 4;
				}
				var ke = ld16_mem8_kernel_read();
				return new[] { ke, le };
			}

			private void init_segment_local_vars()
			{
				CS_base = cpu.segs[1].@base; //CS
				SS_base = cpu.segs[2].@base; //SS
				if ((cpu.segs[2].flags & (1 << 22)) != 0)
					SS_mask = -1;
				else
					SS_mask = 0xffff;
				FS_usage_flag = (((CS_base | SS_base | cpu.segs[3].@base | cpu.segs[0].@base) == 0) && SS_mask == -1);
				if ((cpu.segs[1].flags & (1 << 22)) != 0)
					init_CS_flags = 0;
				else
					init_CS_flags = 0x0100 | 0x0080;
			}

			#endregion
		}

		protected override int exec_internal(uint nCycles, IntNoException interrupt)
		{
			return new Executor(this).exec_internal(nCycles, interrupt);
		}
	}
}