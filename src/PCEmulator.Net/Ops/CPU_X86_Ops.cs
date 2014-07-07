using System;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			internal readonly OperandsHelper Operands;

			public Executor()
			{
				Operands = new OperandsHelper(this);
			}

			private static void ExecOp(Op op)
			{
				op.Exec();
			}


			private Op BuildOp()
			{
				switch (OPbyte)
				{
					case 0x00: //ADD Gb Eb Add
						return BuildArithmeticOp();
					case 0x08: //OR Gb Eb Logical Inclusive OR
						return new OrOp(this, Operands.Eb, Operands.Gb);
					case 0x10: //ADC Gb Eb Add with Carry
						return new AdcOp(this, Operands.Eb, Operands.Gb);
					case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
						return new SbbOp(this, Operands.Eb, Operands.Gb);
					case 0x20: //AND Gb Eb Logical AND
						return new AndOp(this, Operands.Eb, Operands.Gb);
					case 0x28: //SUB Gb Eb Subtract
						return new SubOp(this, Operands.Eb, Operands.Gb);
					case 0x30: //XOR Gb Eb Logical Exclusive OR
						return new XorOp(this, Operands.Eb, Operands.Gb);
					case 0x38: //CMP Eb  Compare Two Operands
						return new CmpOp(this, Operands.Eb, Operands.Gb);

					case 0x02: //ADD Eb Gb Add
						return new AddOp(this, Operands.Gb, Operands.Eb);
					case 0x0a: //OR Eb Gb Logical Inclusive OR
						return new OrOp(this, Operands.Gb, Operands.Eb);
					case 0x12: //ADC Eb Gb Add with Carry
						return new AdcOp(this, Operands.Gb, Operands.Eb);
					case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
						return new SbbOp(this, Operands.Gb, Operands.Eb);
					case 0x22: //AND Eb Gb Logical AND
						return new AndOp(this, Operands.Gb, Operands.Eb);
					case 0x2a: //SUB Eb Gb Subtract
						return new SubOp(this, Operands.Gb, Operands.Eb);
					case 0x32: //XOR Eb Gb Logical Exclusive OR
						return new XorOp(this, Operands.Gb, Operands.Eb);
					case 0x3a: //CMP Gb  Compare Two Operands
						return new CmpOp(this, Operands.Gb, Operands.Eb);
				}
				throw new InvalidOperationException();
			}

			private Op BuildArithmeticOp()
			{
				switch (OPbyte)
				{
					case 0x00: //ADD Gb Eb Add
						return new AddOp(this, Operands.Eb, Operands.Gb);
					case 0x08: //OR Gb Eb Logical Inclusive OR
						return new OrOp(this, Operands.Eb, Operands.Gb);
					case 0x10: //ADC Gb Eb Add with Carry
						return new AdcOp(this, Operands.Eb, Operands.Gb);
					case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
						return new SbbOp(this, Operands.Eb, Operands.Gb);
					case 0x20: //AND Gb Eb Logical AND
						return new AndOp(this, Operands.Eb, Operands.Gb);
					case 0x28: //SUB Gb Eb Subtract
						return new SubOp(this, Operands.Eb, Operands.Gb);
					case 0x30: //XOR Gb Eb Logical Exclusive OR
						return new XorOp(this, Operands.Eb, Operands.Gb);
					case 0x38: //CMP Eb  Compare Two Operands
						return new CmpOp(this, Operands.Eb, Operands.Gb);

					case 0x02: //ADD Eb Gb Add
						return new AddOp(this, Operands.Gb, Operands.Eb);
					case 0x0a: //OR Eb Gb Logical Inclusive OR
						return new OrOp(this, Operands.Gb, Operands.Eb);
					case 0x12: //ADC Eb Gb Add with Carry
						return new AdcOp(this, Operands.Gb, Operands.Eb);
					case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
						return new SbbOp(this, Operands.Gb, Operands.Eb);
					case 0x22: //AND Eb Gb Logical AND
						return new AndOp(this, Operands.Gb, Operands.Eb);
					case 0x2a: //SUB Eb Gb Subtract
						return new SubOp(this, Operands.Gb, Operands.Eb);
					case 0x32: //XOR Eb Gb Logical Exclusive OR
						return new XorOp(this, Operands.Gb, Operands.Eb);
					case 0x3a: //CMP Gb  Compare Two Operands
						return new CmpOp(this, Operands.Gb, Operands.Eb);
				}
				throw new InvalidOperationException();
			}
		}
	}
}