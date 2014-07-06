using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class OperandsHelper
	{
		public readonly JbOperand Jb;
		public readonly IbOperand Ib;
		public readonly IvOperand Iv;
		public readonly EbOperand Eb;
		public readonly EvOperand Ev;
		public readonly GbOperand Gb;
		public readonly RegsOperand RegsCtx;
		public readonly SegmentOperand SegsCtx;

		public OperandsHelper(CPU_X86_Impl.Executor e)
		{
			Jb = new JbOperand(e);
			Iv = new IvOperand(e);
			Ib = new IbOperand(e);
			Eb = new EbOperand(e);
			Ev = new EvOperand(e);
			Gb = new GbOperand(e);
			RegsCtx = new RegsOperand(e);
			SegsCtx = new SegmentOperand(e);
		}
	}
}