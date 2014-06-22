namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			public class JbOpContext : BOpContext
			{
				public JbOpContext(Executor e)
					: base(e)
				{
				}

				public bool check_carry()
				{
					return e.check_carry() != 0;
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
			}

			private void JO(JbOpContext ctx)
			{
				Jump(ctx.check_overflow(), ctx);
			}

			private void JNO(JbOpContext ctx)
			{
				Jump(!ctx.check_overflow(), ctx);
			}

			private void JB(JbOpContext ctx)
			{
				Jump(ctx.check_carry(), ctx);
			}

			private void JNB(JbOpContext ctx)
			{
				Jump(!ctx.check_carry(), ctx);
			}

			private void JZ(JbOpContext ctx)
			{
				Jump(ctx.zeroEquals(), ctx);
			}

			private void JNZ(JbOpContext ctx)
			{
				Jump(!ctx.zeroEquals(), ctx);
			}

			private void JBE(JbOpContext ctx)
			{
				Jump(ctx.check_below_or_equal(), ctx);
			}

			private void JNBE(JbOpContext ctx)
			{
				Jump(!ctx.check_below_or_equal(), ctx);
			}

			private void JS(JbOpContext ctx)
			{
				Jump(ctx.check_sign(), ctx);
			}

			private void JNS(JbOpContext ctx)
			{
				Jump(!ctx.check_sign(), ctx);
			}

			private void JP(JbOpContext ctx)
			{
				Jump(ctx.check_parity(), ctx);
			}

			private void JNP(JbOpContext ctx)
			{
				Jump(!ctx.check_parity(), ctx);
			}

			private void JL(JbOpContext ctx)
			{
				Jump(ctx.check_less_than(), ctx);
			}

			private void JNL(JbOpContext ctx)
			{
				Jump(!ctx.check_less_than(), ctx);
			}

			private void JLE(JbOpContext ctx)
			{
				Jump(ctx.check_less_or_equal(), ctx);
			}

			private void JNLE(JbOpContext ctx)
			{
				Jump(!ctx.check_less_or_equal(), ctx);
			}

			private void Jump(bool doJump, JbOpContext ctx)
			{
				if (doJump)
				{
					x = ctx.readUintByteX();
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