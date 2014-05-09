using System;

namespace PCEmulator.Net
{
	public class PIT
	{
		private IRQCH[] pit_channels;
		private int speaker_data_on;
		private Func<object> set_irq;

		public PIT(PCEmulator PC, Func<object> set_irq_callback, Func<uint> cycle_count_callback)
		{
			this.pit_channels = new IRQCH[3];
			for (var i = 0; i < 3; i++)
			{
				var s = new IRQCH(cycle_count_callback);
				this.pit_channels[i] = s;
				s.mode = 3;
				s.gate = (i != 2) ? 1 : 0;
				s.pit_load_count(0);
			}
			this.speaker_data_on = 0;
			this.set_irq = set_irq_callback;
			// Ports:
			// 0x40: Channel 0 data port
			// 0x61: Control
			PC.register_ioport_write(0x40, 4, 1, this.ioport_write);
			PC.register_ioport_read(0x40, 3, 1, this.ioport_read);
			PC.register_ioport_read(0x61, 1, 1, this.speaker_ioport_read);
			PC.register_ioport_write(0x61, 1, 1, this.speaker_ioport_write);
		}

		public void update_irq()
		{
			throw new NotImplementedException();
		}

		private byte ioport_read(uint mem8Loc)
		{
			throw new NotImplementedException();
		}

		private void ioport_write(uint mem8Loc, byte data)
		{
			throw new NotImplementedException();
		}

		private byte speaker_ioport_read(uint mem8Loc)
		{
			throw new NotImplementedException();
		}

		private void speaker_ioport_write(uint mem8Loc, byte data)
		{
			throw new NotImplementedException();
		}
	}
}