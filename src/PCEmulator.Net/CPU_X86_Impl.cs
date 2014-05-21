using System;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class CPU_X86_Impl : CPU_X86
	{
		private CPU_X86_Impl cpu;
		private uint mem8_loc;

		private uint _src;
		private uint _dst;
		private int _op;

		private uint CS_base;
		private uint SS_base;
		private int SS_mask;
		private bool FS_usage_flag;
		private uint init_CS_flags;
		int[] _tlb_write_;
		private uint CS_flags;


		protected override int exec_internal(uint N_cycles, IntNoException interrupt)
		{
			/*
			  x,y,z,v are either just general non-local values or their exact specialization is unclear,
			  esp. x,y look like they're used for everything

			  I don't know what 'v' should be called, it's not clear yet
			*/
			int _op2;
			int _dst2;
			int mem8;
			int reg_idx0;
			uint OPbyte;
			int reg_idx1;
			uint x;
			uint y;
			object z;
			int conditional_var;
			int exit_code;
			object v;
			object iopl; //io privilege level
			Uint8Array phys_mem8;
			int last_tlb_val;
			object phys_mem16;
			Int32Array phys_mem32;
			int[] tlb_read_kernel;
			int[] tlb_write_kernel;
			int[] tlb_read_user;
			int[] tlb_write_user;

			uint eip;
			int eip_tlb_val;
			uint initial_mem_ptr;
			uint eip_offset;

			cpu = this;
			phys_mem8 = this.phys_mem8;
			phys_mem16 = this.phys_mem16;
			phys_mem32 = this.phys_mem32;
			tlb_read_user = this.tlb_read_user;
			tlb_write_user = this.tlb_write_user;
			tlb_read_kernel = this.tlb_read_kernel;
			tlb_write_kernel = this.tlb_write_kernel;

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

			regs = this.regs;
			_src = (uint) cc_src;
			_dst = (uint) cc_dst;
			_op = cc_op;
			_op2 = cc_op2;
			_dst2 = cc_dst2;

			eip = (uint) this.eip;
			init_segment_local_vars();
			exit_code = 256;
			var cycles_left = N_cycles;

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
							initial_mem_ptr = physmem8_ptr = mem_size;
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

				OPbyte |= (CS_flags = init_CS_flags) & 0x0100; //Are we running in 16bit compatibility mode? if so, ops look like 0x1XX instead of 0xXX
				//TODO: implement EXEC_LOOP
				EXEC_LOOP:
				for (;;)
				{
					switch (OPbyte)
					{
						case 0x00: //ADD Gb Eb Add
						case 0x08: //OR Gb Eb Logical Inclusive OR
						case 0x10: //ADC Gb Eb Add with Carry
						case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
						case 0x20: //AND Gb Eb Logical AND
						case 0x28: //SUB Gb Eb Subtract
						case 0x30: //XOR Gb Eb Logical Exclusive OR
						case 0x38: //CMP Eb  Compare Two Operands
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int) (OPbyte >> 3);
							reg_idx1 = (mem8 >> 3) & 7;
							y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								set_word_in_register(reg_idx0, do_8bit_math(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), y));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
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
						case 0x01: //ADD Gvqp Evqp Add
							mem8 = phys_mem8[physmem8_ptr++];
							y = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								{
									_src = y;
									_dst = regs[reg_idx0] = (regs[reg_idx0] + _src) >> 0;
									_op = 2;
								}
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_write();
								{
									_src = y;
									_dst = x = (x + _src) >> 0;
									_op = 2;
								}
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0x50: //PUSH Zv SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x51:
						case 0x52:
						case 0x53:
						case 0x54:
						case 0x55:
						case 0x56:
						case 0x57:
							x = regs[OPbyte & 7];
							if (FS_usage_flag)
							{
								mem8_loc = (regs[4] - 4) >> 0;
								{
									last_tlb_val = _tlb_write_[mem8_loc >> 12];
									if (((last_tlb_val | mem8_loc) & 3) != 0)
									{
										__st32_mem8_write(x);
									}
									else
									{
										phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int) x;
									}
								}
								regs[4] = mem8_loc;
							}
							else
							{
								push_dword_to_stack(x);
							}
							goto EXEC_LOOP_END;
						case 0x58: //POP SS:[rSP] Zv Pop a Value from the Stack
						case 0x59:
						case 0x5a:
						case 0x5b:
						case 0x5c:
						case 0x5d:
						case 0x5e:
						case 0x5f:
							if (FS_usage_flag)
							{
								mem8_loc = regs[4];
								x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
									? __ld_32bits_mem8_read()
									: (uint) phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
								regs[4] = (mem8_loc + 4) >> 0;
							}
							else
							{
								x = pop_dword_from_stack_read();
								pop_dword_from_stack_incr_ptr();
							}
							regs[OPbyte & 7] = x;
							goto EXEC_LOOP_END;

						case 0x74: //JZ Jbs  Jump short if zero/equal (ZF=0)
							if ((_dst == 0))
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x75: //JNZ Jbs  Jump short if not zero/not equal (ZF=1)
							if (_dst != 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x83: //ADD Ibs Evqp Add
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if (conditional_var == 7)
							{
								if ((mem8 >> 6) == 3)
								{
									x = regs[mem8 & 7];
								}
								else
								{
									mem8_loc = segment_translation(mem8);
									x = ld_32bits_mem8_read();
								}
								y = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								{
									_src = y;
									_dst = ((x - _src) >> 0);
									_op = 8;
								}
							}
							else
							{
								if ((mem8 >> 6) == 3)
								{
									reg_idx0 = mem8 & 7;
									y = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
									regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
								}
								else
								{
									mem8_loc = segment_translation(mem8);
									y = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
									x = ld_32bits_mem8_write();
									x = do_32bit_math(conditional_var, x, y);
									st32_mem8_write(x);
								}
							}
							goto EXEC_LOOP_END;
						case 0x84: //TEST Eb  Logical Compare
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_8bits_mem8_read();
							}
							reg_idx1 = (mem8 >> 3) & 7;
							y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
						{
							_dst = (((x & y) << 24) >> 24);
							_op = 12;
						}
							goto EXEC_LOOP_END;
						case 0x89: //MOV Gvqp Evqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							x = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								regs[mem8 & 7] = x;
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								{
									last_tlb_val = _tlb_write_[mem8_loc >> 12];
									if (((last_tlb_val | mem8_loc) & 3) != 0)
									{
										__st32_mem8_write(x);
									}
									else
									{
										phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int) x;
									}
								}
							}
							goto EXEC_LOOP_END;
						case 0x8b: //MOV Evqp Gvqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								x = regs[mem8 & 7];
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
									? __ld_32bits_mem8_read()
									: (uint) phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
							}
							regs[(mem8 >> 3) & 7] = x;
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
						case 0xb8: //MOV Ivqp Zvqp Move
						case 0xb9:
						case 0xba:
						case 0xbb:
						case 0xbc:
						case 0xbd:
						case 0xbe:
						case 0xbf:
							{
								x = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											(phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
							regs[OPbyte & 7] = x;
							goto EXEC_LOOP_END;

						case 0xc7: //MOV Ivds Evqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								{
									x = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
									            (phys_mem8[physmem8_ptr + 3] << 24));
									physmem8_ptr += 4;
								}
								regs[mem8 & 7] = x;
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								{
									x = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
									            (phys_mem8[physmem8_ptr + 3] << 24));
									physmem8_ptr += 4;
								}
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xe8: //CALL Jvds SS:[rSP] Call Procedure
						{
							x = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
							            (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
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
							physmem8_ptr = (physmem8_ptr + x) >> 0;
							goto EXEC_LOOP_END;

						/*
							TWO BYTE CODE INSTRUCTIONS BEGIN WITH 0F :  0F xx
							=====================================================================================================
						*/
						case 0x0f:
							OPbyte = phys_mem8[physmem8_ptr++];
							switch (OPbyte)
							{
								case 0xb6: //MOVZX Eb Gvqp Move with Zero-Extend
									mem8 = phys_mem8[physmem8_ptr++];
									reg_idx1 = (mem8 >> 3) & 7;
									if ((mem8 >> 6) == 3)
									{
										reg_idx0 = mem8 & 7;
										x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)) & 0xff;
									}
									else
									{
										mem8_loc = segment_translation(mem8);
										x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
											? __ld_8bits_mem8_read()
											: phys_mem8[mem8_loc ^ last_tlb_val]);
									}
									regs[reg_idx1] = x;
									goto EXEC_LOOP_END;

								default:
									throw new NotImplementedException(string.Format("OPbyte 0x0f 0x{0:X} not implemented", OPbyte));
							}
							break;
						default:
							throw new NotImplementedException(string.Format("OPbyte 0x{0:X} not implemented", OPbyte));
					}
				}

			EXEC_LOOP_END:
				{}
			} while (--cycles_left != 0); //End Giant Core DO WHILE Execution Loop
			cycle_count += (N_cycles - cycles_left);
			this.eip = eip + physmem8_ptr - initial_mem_ptr;
			cc_src = (int) _src;
			cc_dst = (int) _dst;
			cc_op = _op;
			cc_op2 = _op2;
			cc_dst2 = _dst2;
			return exit_code;
		}

		private void pop_dword_from_stack_incr_ptr()
		{
			throw new NotImplementedException();
		}

		private uint pop_dword_from_stack_read()
		{
			throw new NotImplementedException();
		}

		private uint __ld_32bits_mem8_read()
		{
			throw new NotImplementedException();
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
					_src = Zb;
					Yb = (Yb + Zb) >> 0;
					_dst = Yb;
					_op = 2;
					break;
				case 1:
					Yb = Yb | Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 2:
					ac = check_carry();
					_src = Zb;
					Yb = (Yb + Zb + ac) >> 0;
					_dst = Yb;
					_op = ac != 0 ? 5 : 2;
					break;
				case 3:
					ac = check_carry();
					_src = Zb;
					Yb = (Yb - Zb - ac) >> 0;
					_dst = Yb;
					_op = ac != 0? 11 : 8;
					break;
				case 4:
					Yb = Yb & Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 5:
					_src = Zb;
					Yb = (Yb - Zb) >> 0;
					_dst = Yb;
					_op = 8;
					break;
				case 6:
					Yb = Yb ^ Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 7:
					_src = Zb;
					_dst = (Yb - Zb) >> 0;
					_op = 8;
					break;
				default:
					throw new Exception("arith" + ac + ": invalid op");
			}
			return Yb;
		}

		private uint check_carry()
		{
			throw new NotImplementedException();
		}

		private uint ld_32bits_mem8_read()
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

		private void st32_mem8_write(uint x)
		{
			int last_tlb_val;
			{
				last_tlb_val = _tlb_write_[mem8_loc >> 12];
				if (((last_tlb_val | mem8_loc) & 3) != 0)
				{
					__st32_mem8_write(x);
				}
				else
				{
					phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int) x;
				}
			}
		}

		private void push_dword_to_stack(uint i)
		{
			throw new NotImplementedException();
		}

		private void __st32_mem8_write(uint x)
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

		private uint ld_8bits_mem8_read()
		{
			var last_tlb_val = _tlb_read_[mem8_loc >> 12];
			return ((last_tlb_val == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
		}

		private void st8_mem8_write(uint x)
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
					phys_mem8[mem8_loc ^ last_tlb_val] = (byte) x;
				}
			}
		}

		private void __st8_mem8_write(uint x)
		{
			do_tlb_set_page(mem8_loc, true, cpu.cpl == 3);
			var tlb_lookup = _tlb_write_[mem8_loc >> 12] ^ mem8_loc;
			phys_mem8[tlb_lookup] = (byte) x;
		}

		private uint ld_8bits_mem8_write()
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

		private uint physmem8_ptr;
		private object eip_tlb_val;
		private object initial_mem_ptr;
		private object eip_offset;
		private int[] _tlb_read_;

		/// <summary>
		/// segment translation routine (I believe):
		/// Translates Logical Memory Address to Linear Memory Address
		/// </summary>
		private uint segment_translation(int mem8)
		{
			int @base;
			int mem8_loc;
			int Qb;
			int Rb;
			int Sb;
			object Tb;
			if (FS_usage_flag && (CS_flags & (0x000f | 0x0080)) == 0)
			{
				switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
				{
					case 0x04:
						Qb = phys_mem8[physmem8_ptr++];
						@base = Qb & 7;
						if (@base == 5)
						{
							{
								mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
								           (phys_mem8[physmem8_ptr + 3] << 24);
								physmem8_ptr += 4;
							}
						}
						else
						{
							mem8_loc = (int) regs[@base];
						}
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x0c:
						Qb = phys_mem8[physmem8_ptr++];
						mem8_loc = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = Qb & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x14:
						Qb = phys_mem8[physmem8_ptr++];
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						@base = Qb & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x05:
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						break;
					case 0x00:
					case 0x01:
					case 0x02:
					case 0x03:
					case 0x06:
					case 0x07:
						@base = mem8 & 7;
						mem8_loc = (int) regs[@base];
						break;
					case 0x08:
					case 0x09:
					case 0x0a:
					case 0x0b:
					case 0x0d:
					case 0x0e:
					case 0x0f:
						mem8_loc = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = mem8 & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						break;
					case 0x10:
					case 0x11:
					case 0x12:
					case 0x13:
					case 0x15:
					case 0x16:
					case 0x17:
					default:
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						@base = mem8 & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						break;
				}
				return (uint) mem8_loc;
			}
			else if ((CS_flags & 0x0080) != 0)
			{
				if ((mem8 & 0xc7) == 0x06)
				{
					mem8_loc = ld16_mem8_direct();
					Tb = 3;
				}
				else
				{
					switch (mem8 >> 6)
					{
						case 0:
							mem8_loc = 0;
							break;
						case 1:
							mem8_loc = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
							break;
						default:
							mem8_loc = ld16_mem8_direct();
							break;
					}
					switch (mem8 & 7)
					{
						case 0:
							mem8_loc = (int) ((mem8_loc + regs[3] + regs[6]) & 0xffff);
							Tb = 3;
							break;
						case 1:
							mem8_loc = (int) ((mem8_loc + regs[3] + regs[7]) & 0xffff);
							Tb = 3;
							break;
						case 2:
							mem8_loc = (int) ((mem8_loc + regs[5] + regs[6]) & 0xffff);
							Tb = 2;
							break;
						case 3:
							mem8_loc = (int) ((mem8_loc + regs[5] + regs[7]) & 0xffff);
							Tb = 2;
							break;
						case 4:
							mem8_loc = (int) ((mem8_loc + regs[6]) & 0xffff);
							Tb = 3;
							break;
						case 5:
							mem8_loc = (int) ((mem8_loc + regs[7]) & 0xffff);
							Tb = 3;
							break;
						case 6:
							mem8_loc = (int) ((mem8_loc + regs[5]) & 0xffff);
							Tb = 2;
							break;
						case 7:
						default:
							mem8_loc = (int) ((mem8_loc + regs[3]) & 0xffff);
							Tb = 3;
							break;
					}
				}
				Sb = (int) (CS_flags & 0x000f);
				if (Sb == 0)
				{
					Sb = (int) Tb;
				}
				else
				{
					Sb--;
				}
				mem8_loc = (int) ((mem8_loc + cpu.segs[Sb].@base) >> 0);
				return (uint) mem8_loc;
			}
			else
			{
				switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
				{
					case 0x04:
						Qb = phys_mem8[physmem8_ptr++];
						@base = Qb & 7;
						if (@base == 5)
						{
							{
								mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
								           (phys_mem8[physmem8_ptr + 3] << 24);
								physmem8_ptr += 4;
							}
							@base = 0;
						}
						else
						{
							mem8_loc = (int) regs[@base];
						}
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x0c:
						Qb = phys_mem8[physmem8_ptr++];
						mem8_loc = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = Qb & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x14:
						Qb = phys_mem8[physmem8_ptr++];
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						@base = Qb & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = (int) ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x05:
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						@base = 0;
						break;
					case 0x00:
					case 0x01:
					case 0x02:
					case 0x03:
					case 0x06:
					case 0x07:
						@base = mem8 & 7;
						mem8_loc = (int) regs[@base];
						break;
					case 0x08:
					case 0x09:
					case 0x0a:
					case 0x0b:
					case 0x0d:
					case 0x0e:
					case 0x0f:
						mem8_loc = ((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = mem8 & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						break;
					case 0x10:
					case 0x11:
					case 0x12:
					case 0x13:
					case 0x15:
					case 0x16:
					case 0x17:
					default:
					{
						mem8_loc = phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
						           (phys_mem8[physmem8_ptr + 3] << 24);
						physmem8_ptr += 4;
					}
						@base = mem8 & 7;
						mem8_loc = (int) ((mem8_loc + regs[@base]) >> 0);
						break;
				}
				Sb = (int) (CS_flags & 0x000f);
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
				mem8_loc = (int) ((mem8_loc + cpu.segs[Sb].@base) >> 0);
				return (uint) mem8_loc;
			}
		}

		private int ld16_mem8_direct()
		{
			throw new NotImplementedException();
		}

		private void set_word_in_register(int regIdx0, uint do_8BitMath)
		{
			throw new NotImplementedException();
		}

		private uint do_8bit_math(int conditional_var, uint Yb, uint Zb)
		{
			uint ac = 0;
			switch (conditional_var)
			{
				case 0:
					_src = Zb;
					Yb = (((Yb + Zb) << 24) >> 24);
					_dst = Yb;
					_op = 0;
					break;
				case 1:
					Yb = (((Yb | Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 2:
					ac = check_carry();
					_src = Zb;
					Yb = (((Yb + Zb + ac) << 24) >> 24);
					_dst = Yb;
					_op = ac != 0 ? 3 : 0;
					break;
				case 3:
					ac = check_carry();
					_src = Zb;
					Yb = (((Yb - Zb - ac) << 24) >> 24);
					_dst = Yb;
					_op = ac != 0 ? 9 : 6;
					break;
				case 4:
					Yb = (((Yb & Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 5:
					_src = Zb;
					Yb = (((Yb - Zb) << 24) >> 24);
					_dst = Yb;
					_op = 6;
					break;
				case 6:
					Yb = (((Yb ^ Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 7:
					_src = Zb;
					_dst = (((Yb - Zb) << 24) >> 24);
					_op = 6;
					break;
				default:
					throw new Exception("arith" + ac + ": invalid op");
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
			var tlb_lookup = _tlb_read_[mem8_loc >> 12] ^ mem8_loc;
			return phys_mem8[tlb_lookup];
		}

		private uint operation_size_function(uint eipOffset, object oPbyte)
		{
			//throw new NotImplementedException();
			//TODO: implement operation_size_function
			return 0;
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
			{ //CR0: bit31 PG Paging If 1, enable paging and use the CR3 register, else disable paging
				cpu.tlb_set_page((int) (Gd & -4096), (int) (Gd & -4096), true);
			}
			else
			{
				var Id = (uint) ((cpu.cr3 & -4096) + ((Gd >> 20) & 0xffc));
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
					var Kd = (uint) ((Jd & -4096) + ((Gd >> 10) & 0xffc));
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
							cpu.tlb_set_page((int) (Gd & -4096), Ld & -4096, ud, Od);
							return;
						}
					}
				}
				error_code |= (Hd ? 1 : 0) << 1;
				if (ja)
					error_code |= 0x04;
				cpu.cr2 = (int) Gd;
				abort_with_error_code(14, error_code);
			}
		}

		private void abort_with_error_code(int i, int errorCode)
		{
			throw new NotImplementedException();
		}

		private void tlb_set_page(int mem8_loc, int page_val, bool set_write_tlb, bool set_user_tlb = false)
		{
			page_val &= -4096; // only top 20bits matter
			mem8_loc &= -4096; // only top 20bits matter
			var x = mem8_loc ^ page_val; // XOR used to simulate hashing 
			var i = mem8_loc >> 12;
			if (this.tlb_read_kernel[i] == -1)
			{
				if (this.tlb_pages_count >= 2048)
				{
					this.tlb_flush_all1((i - 1) & 0xfffff);
				}
				this.tlb_pages[this.tlb_pages_count++] = i;
			}
			this.tlb_read_kernel[i] = x;
			if (set_write_tlb)
			{
				this.tlb_write_kernel[i] = x;
			}
			else
			{
				this.tlb_write_kernel[i] = -1;
			}
			if (set_user_tlb)
			{
				this.tlb_read_user[i] = x;
				if (set_write_tlb)
				{
					this.tlb_write_user[i] = x;
				}
				else
				{
					this.tlb_write_user[i] = -1;
				}
			}
			else
			{
				this.tlb_read_user[i] = -1;
				this.tlb_write_user[i] = -1;
			}
		}

		private void tlb_flush_all1(long l)
		{
			throw new NotImplementedException();
		}

		private void do_interrupt(int intno, int p1, int errorCode, int p3, int p4)
		{
			throw new NotImplementedException();
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
	}
}