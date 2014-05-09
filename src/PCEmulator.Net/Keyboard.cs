using System;

namespace PCEmulator.Net
{
	/// <summary>
	/// Keyboard Device Emulator
	/// </summary>
	public class Keyboard
	{
		private readonly Action resetRequest;

		public Keyboard(PCEmulator pc, Action resetCallback)
		{
			pc.register_ioport_read(0x64, 1, 1, ioport_read);
			pc.register_ioport_write(0x64, 1, 1, ioport_write);
			resetRequest = resetCallback;
		}

		private byte ioport_read(uint mem8Loc)
		{
			return 0;
		}

		private void ioport_write(uint mem8Loc, byte data)
		{
			switch (data)
			{
				case 0xfe: // Resend command. Other commands are, apparently, ignored.
					resetRequest();
					break;
			}
		}
	}
}