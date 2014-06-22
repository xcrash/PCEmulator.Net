namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			/// <summary>
			/// The instruction contains a relative offset to be added to the address of the subsequent instruction. Applicable, e.g., to short JMP (opcode EB), or LOOP.
			/// </summary>
			public class JbOperand : Operand<byte>
			{
				private readonly Executor e;

				public JbOperand(Executor e)
					: base(new BArgument(e), e)
				{
					this.e = e;
				}

				public bool check_overflow()
				{
					return e.check_overflow() != 0;
				}

				public bool check_carry()
				{
					return e.check_carry() != 0;
				}

				public bool zeroEquals()
				{
					return e.u_dst == 0;
				}

				public bool check_below_or_equal()
				{
					return e.check_below_or_equal();
				}

				public bool check_sign()
				{
					return (e._op == 24 ? (int)((e._src >> 7) & 1) : ((int)e._dst < 0 ? 1 : 0)) != 0;
				}

				public bool check_parity()
				{
					return e.check_parity() != 0;
				}

				public bool check_less_than()
				{
					return e.check_less_than();
				}

				public bool check_less_or_equal()
				{
					return e.check_less_or_equal();
				}

				public new uint readX()
				{
					return (uint)((base.readX() << 24) >> 24);
				}

				public override byte PopValue()
				{
					throw new System.NotImplementedException();
				}

				public override void PushValue(byte x)
				{
					throw new System.NotImplementedException();
				}
			}

			private void JO(JbOperand ctx)
			{
				Jump(ctx.check_overflow(), ctx);
			}

			private void JNO(JbOperand ctx)
			{
				Jump(!ctx.check_overflow(), ctx);
			}

			private void JB(JbOperand ctx)
			{
				Jump(ctx.check_carry(), ctx);
			}

			private void JNB(JbOperand ctx)
			{
				Jump(!ctx.check_carry(), ctx);
			}

			private void JZ(JbOperand ctx)
			{
				Jump(ctx.zeroEquals(), ctx);
			}

			private void JNZ(JbOperand ctx)
			{
				Jump(!ctx.zeroEquals(), ctx);
			}

			private void JBE(JbOperand ctx)
			{
				Jump(ctx.check_below_or_equal(), ctx);
			}

			private void JNBE(JbOperand ctx)
			{
				Jump(!ctx.check_below_or_equal(), ctx);
			}

			private void JS(JbOperand ctx)
			{
				Jump(ctx.check_sign(), ctx);
			}

			private void JNS(JbOperand ctx)
			{
				Jump(!ctx.check_sign(), ctx);
			}

			private void JP(JbOperand ctx)
			{
				Jump(ctx.check_parity(), ctx);
			}

			private void JNP(JbOperand ctx)
			{
				Jump(!ctx.check_parity(), ctx);
			}

			private void JL(JbOperand ctx)
			{
				Jump(ctx.check_less_than(), ctx);
			}

			private void JNL(JbOperand ctx)
			{
				Jump(!ctx.check_less_than(), ctx);
			}

			private void JLE(JbOperand ctx)
			{
				Jump(ctx.check_less_or_equal(), ctx);
			}

			private void JNLE(JbOperand ctx)
			{
				Jump(!ctx.check_less_or_equal(), ctx);
			}

			private void Jump(bool doJump, JbOperand ctx)
			{
				if (doJump)
				{
					x = ctx.readX();
					Jump(x);
				}
				else
					Jump(1);
			}

			private void Jump(uint x)
			{
				physmem8_ptr = (physmem8_ptr + x) >> 0;
			}
		}
	}
}