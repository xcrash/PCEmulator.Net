using System;
using PCEmulator.Net.KnowledgeBase;
using PCEmulator.Net.Operands;

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
					case 0x08: //OR Gb Eb Logical Inclusive OR
					case 0x10: //ADC Gb Eb Add with Carry
					case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
					case 0x20: //AND Gb Eb Logical AND
					case 0x28: //SUB Gb Eb Subtract
					case 0x30: //XOR Gb Eb Logical Exclusive OR
					case 0x38: //CMP Eb  Compare Two Operands

					case 0x02: //ADD Eb Gb Add
					case 0x0a: //OR Eb Gb Logical Inclusive OR
					case 0x12: //ADC Eb Gb Add with Carry
					case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
					case 0x22: //AND Eb Gb Logical AND
					case 0x2a: //SUB Eb Gb Subtract
					case 0x32: //XOR Eb Gb Logical Exclusive OR
					case 0x3a: //CMP Gb  Compare Two Operands
						return BuildArithmeticOp();
				}
				throw new InvalidOperationException();
			}

			private Op BuildArithmeticOp()
			{
				IOperand<byte> o0;
				IOperand<byte> o1;

				switch (OPbyte.GetDirection())
				{
					case OpDirection.RegistryToMemory:
						o0 = Operands.Eb;
						o1 = Operands.Gb;
						break;
					case OpDirection.MemoryToRegistry:
						o0 = Operands.Gb;
						o1 = Operands.Eb;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				switch (OPbyte.GetArithmeticOpType())
				{
					case OpArithmeticType.Add:
						return new AddOp(this, o0, o1);
					case OpArithmeticType.Or:
						return new OrOp(this, o0, o1);
					case OpArithmeticType.Adc:
						return new AdcOp(this, o0, o1);
					case OpArithmeticType.Sbb:
						return new SbbOp(this, o0, o1);
					case OpArithmeticType.And:
						return new AndOp(this, o0, o1);
					case OpArithmeticType.Sub:
						return new SubOp(this, o0, o1);
					case OpArithmeticType.Xor:
						return new XorOp(this, o0, o1);
					case OpArithmeticType.Cmp:
						return new CmpOp(this, o0, o1);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}
	}
}