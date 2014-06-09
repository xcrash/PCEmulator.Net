using System;
using System.Text;
using System.Windows.Forms;

namespace PCEmulator.Js.Wrapper
{
	public class JsDebugLogReceiver
	{
		private readonly IJsHostHandler jsHostHandler;
		private readonly TextBox textBox;

		public JsDebugLogReceiver(TextBox textBox)
		{
			this.textBox = textBox;
		}

		public JsDebugLogReceiver(IJsHostHandler jsHostHandler)
		{
			this.jsHostHandler = jsHostHandler;
		}

		public JsDebugLogReceiver(IJsHostHandler jsHostHandler, TextBox textBox)
		{
			this.textBox = textBox;
			this.jsHostHandler = jsHostHandler;
		}

		// ReSharper disable once InconsistentNaming
		public void onDebugLog(string log)
		{
			if (textBox != null)
			{
				if (textBox.InvokeRequired)
					textBox.Invoke(new Action(() => AddDebugLog(log)));
				else
				{
					AddDebugLog(log);
				}
			}

			if (jsHostHandler != null)
				jsHostHandler.DebugLog(log);
		}

		private void AddDebugLog(string log)
		{
			var newText = new StringBuilder(textBox.Text);
			newText.AppendLine(log);

			const int max = 512;
			if (newText.Length > max)
				newText.Remove(0, newText.Length - max);

			textBox.Text = newText.ToString();
		}
	}
}