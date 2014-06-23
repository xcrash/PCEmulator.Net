namespace PCEmulator.Net.Operands.Args
{
	public interface IArgumentOperand<T>
	{
		T readX();
		T setX { set; }
	}
}