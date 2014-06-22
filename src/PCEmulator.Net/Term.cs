using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PCEmulator.Net
{
	public class Term : IDisposable
	{
		private readonly int height;
		private readonly Action<char> termHandler;
		private int width;

		private int row;
		private int column;

		private char[] buffer;
		private char[][] lines;
		private int scrollTop, scrollBottom;
		private List<int> tabStops;
		private int saveRow, saveColumn;

		private string escapeChars, escapeArgs;

		private bool started;
		private bool vt52Mode;
		private bool autoWrapMode;
		private readonly int defaultWidth;

		private int state = 0;

		private int lastCol = -1;
		private int lastRow = -1;

		public Term(int cols, int rows, Action<char> termHandler)
		{
			height = rows;
			defaultWidth = cols;
			this.termHandler = termHandler;

			Reset();
			BeginReadFromConsole();
		}

		private void Reset()
		{
			width = defaultWidth;
			row = 0;
			column = 0;
			scrollTop = 0;
			scrollBottom = height;
			vt52Mode = false;
			autoWrapMode = true;
			tabStops = new List<int>();
			saveRow = saveColumn = 0;
			ResetBufferAndWindows();
		}

		private void ResetBuffer()
		{
			buffer = (from i in Enumerable.Range(0, width*(height+1)) select new char()).ToArray();
			UpdateLines();
		}

		private void ResetBufferAndWindows()
		{
			ResetBuffer();
			Console.ResetColor();
			Console.SetWindowSize(width, height);
			Console.SetBufferSize(width, height);
		}

		public void Write(char ch)
		{
			if (escapeChars != null)
			{
				ProcessEscapeCharacter(ch);
				return;
			}

			const int za = 0;
			const int Aa = 1;
			const int Ba = 2;

			switch (state)
			{
				case za:
					switch ((int)ch)
					{
						case 27:
							state = Aa;
							break;

					}
					break;
				case Aa:
					state = ch == 91 ? Ba : za;
					break;
				case Ba:
					if (ch >= 48 && ch <= 57)
					{
					}
					else
					{
						if (ch == 59)
							break;
						state = za;
					}
					break;
			}

			switch (ch)
			{
				case '\x1b':
					escapeChars = "";
					escapeArgs = "";
					break;
				case '\r':
					column = 0;
					break;
				case '\n':
					NextRowWithScroll();
					break;

				case '\t':
					column = (from stop in tabStops where stop > column select (int?) stop).Min() ?? width - 1;
					break;

				default:
					BufferAt(row, column, ch);

					if (++column >= width)
						if (autoWrapMode)
						{
							column = 0;
							NextRowWithScroll();
						}
						else
							column--;
					break;
			}
		}

		private void BufferAt(int row, int column, char x)
		{
			if (row < 0 || row >= height || column < 0 || column >= width)
				return;

			if (column != width - 1 && row != height - 1)
			{
				if(lastCol != column || lastRow + 1 != row)
					Console.SetCursorPosition(column, row);
				Console.Write(x);
				lastCol = column;
				lastRow = row;
			}
			else
			{
				//TODO: what to do with bottom right char?
				//see: http://stackoverflow.com/questions/739526/disabling-scroll-with-system-console-write
			}
			buffer[row * width + column] = x;
		}

		public void Dispose()
		{
			started = false;
		}

		private void ProcessEscapeCharacter(char ch)
		{
			if (escapeChars.Length == 0 && "78".IndexOf(ch) >= 0)
			{
				escapeChars += ch.ToString(CultureInfo.InvariantCulture);
			}
			else if (escapeChars.Length > 0 && "()Y".IndexOf(escapeChars[0]) >= 0)
			{
				escapeChars += ch.ToString(CultureInfo.InvariantCulture);
				if (escapeChars.Length != (escapeChars[0] == 'Y' ? 3 : 2)) return;
			}
			else if (ch == ';' || char.IsDigit(ch))
			{
				escapeArgs += ch.ToString(CultureInfo.InvariantCulture);
				return;
			}
			else
			{
				escapeChars += ch.ToString(CultureInfo.InvariantCulture);
				if ("[#?()Y".IndexOf(ch) >= 0) return;
			}
			ProcessEscapeSequence();
			escapeChars = null;
			escapeArgs = null;
		}

		private void ProcessEscapeSequence()
		{
			if (escapeChars.StartsWith("Y"))
			{
				row = escapeChars[1] - 64;
				column = escapeChars[2] - 64;
				return;
			}
			if (vt52Mode && (escapeChars == "D" || escapeChars == "H")) escapeChars += "_";

			var args = escapeArgs.Split(';');
			var arg0 = args.Length > 0 && args[0] != "" ? int.Parse(args[0]) : (int?) null;
			switch (escapeChars)
			{
				case "[A":
				case "A":
					row -= Math.Max(arg0 ?? 1, 1);
					break;
				case "[B":
				case "B":
					row += Math.Max(arg0 ?? 1, 1);
					break;
				case "[c":
				case "C":
					column += Math.Max(arg0 ?? 1, 1);
					break;
				case "[D":
				case "D":
					column -= Math.Max(arg0 ?? 1, 1);
					break;

				case "[f":
				case "[H":
				case "H_":
					row = Math.Max(arg0 ?? 1, 1) - 1;
					column = Math.Max(arg0 ?? 1, 1) - 1;
					break;

				case "M":
					PriorRowWithScroll();
					break;
				case "D_":
					NextRowWithScroll();
					break;
				case "E":
					NextRowWithScroll();
					column = 0;
					break;

				case "[r":
					scrollTop = (arg0 ?? 1) - 1;
					scrollBottom = (arg0 ?? height);
					break;

				case "H":
					if (!tabStops.Contains(column)) tabStops.Add(column);
					break;
				case "g":
					if (arg0 == 3) tabStops.Clear();
					else tabStops.Remove(column);
					break;

				case "[J":
				case "J":
					switch (arg0 ?? 0)
					{
						case 0:
							ClearRange(row, column, height, width);
							break;
						case 1:
							ClearRange(0, 0, row, column + 1);
							break;
						case 2:
							ClearRange(0, 0, height, width);
							break;
					}
					break;
				case "[K":
				case "K":
					switch (arg0 ?? 0)
					{
						case 0:
							ClearRange(row, column, row, width);
							break;
						case 1:
							ClearRange(row, 0, row, column + 1);
							break;
						case 2:
							ClearRange(row, 0, row, width);
							break;
					}
					break;

				case "?l":
				case "?h":
					var h = escapeChars == "?h";
					switch (arg0)
					{
						case 2:
							vt52Mode = h;
							break;
						case 3:
							width = h ? 132 : 80;
							ResetBufferAndWindows();
							break;
						case 7:
							autoWrapMode = h;
							break;
					}
					break;
				case "<":
					vt52Mode = false;
					break;

				case "#3":
				case "#4":
				case "#5":
				case "#6":
					break;

				case "[s":
					saveRow = row;
					saveColumn = column;
					break;
				case "7":
					saveRow = row;
					saveColumn = column;
					break;
				case "[u":
					row = saveRow;
					column = saveColumn;
					break;
				case "8":
					row = saveRow;
					column = saveColumn;
					break;

				case "c":
					Reset();
					break;
			}
			if (column < 0) column = 0;
			if (column >= width) column = width - 1;
			if (row < 0) row = 0;
			if (row >= height) row = height - 1;
		}

		private void PriorRowWithScroll()
		{
			if (row == scrollTop) ScrollDown();
			else row--;
		}

		private void NextRowWithScroll()
		{
			if (row == scrollBottom - 1) ScrollUp();
			else row++;
		}

		private void ScrollUp()
		{
			Array.Copy(buffer, width*(scrollTop + 1), buffer, width*scrollTop, width*(scrollBottom - scrollTop - 1));
			for (var r = 0; r < height-1; r++)
			{
				var line = new string(buffer.Skip(r * width).Take(width).Select(x => x == 0 ? ' ' : x).ToArray());
				Console.SetCursorPosition(0, r);
				Console.Write(line);
			}

			ClearRange(scrollBottom - 1, 0, scrollBottom - 1, width);
			UpdateLines();
		}

		private void ScrollDown()
		{
			Array.Copy(buffer, width*scrollTop, buffer, width*(scrollTop + 1), width*(scrollBottom - scrollTop - 1));
			ClearRange(scrollTop, 0, scrollTop, width);
			UpdateLines();
		}

		private void ClearRange(int startRow, int startColumn, int endRow, int endColumn)
		{
			var start = startRow*width + startColumn;
			var end = endRow*width + endColumn;
			for (var i = start; i < end; i++)
			{
				const char empty = ' ';
				buffer[i] = empty;

				var col = i%width;
				var row = i/width;
				BufferAt(row, col, empty);
			}
		}

		private void UpdateLines()
		{
			lines = new char[height][];
			for (var r = 0; r < height; r++)
			{
				lines[r] = new char[width];
				Array.Copy(buffer, r*height, lines[r], 0, width);
			}
		}

		private void BeginReadFromConsole()
		{
			started = true;
			Console.TreatControlCAsInput = true;
			Task.Factory.StartNew(() =>
			{
				while (started)
				{
					var consoleKeyInfo = Console.ReadKey(true);
					keyDownHandler(consoleKeyInfo);
				}
			}, TaskCreationOptions.LongRunning);
		}

		private void keyDownHandler(ConsoleKeyInfo @event)
		{
			string @char = string.Empty;
			switch (@event.Key)
			{
				case ConsoleKey.Tab:
				case ConsoleKey.Backspace:
				case ConsoleKey.Enter:
					@char = string.Empty + (char)@event.Key;
					break;
				case ConsoleKey.Escape:
					@char = "\x1b";
					break;
				case ConsoleKey.LeftArrow:
					@char = "\x1b[D";
					break;
				case ConsoleKey.RightArrow:
					@char = "\x1b[C";
					break;
				case ConsoleKey.UpArrow:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						ScrollDown();
					else
						@char = "\x1b[A";
					break;
				case ConsoleKey.DownArrow:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						ScrollUp();
					else
						@char = "\x1b[B";
					break;
				case ConsoleKey.Delete:
					@char = "\x1b[3~";
					break;
				case ConsoleKey.Insert:
					@char = "\x1b[2~";
					break;
				case ConsoleKey.Home:
					@char = "\x1bOH";
					break;
				case ConsoleKey.End:
					@char = "\x1bOF";
					break;
				case ConsoleKey.PageUp:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						ScrollUp(); //TODO: this.scroll_disp(-(this.h - 1));
					else
						@char = "\x1b[5~";
					break;
				case ConsoleKey.PageDown:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						ScrollDown(); //TODO: this.scroll_disp(this.h - 1);
					else
						@char = "\x1b[6~";
					break;
				default:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
					{
						if (@event.KeyChar >= 65 - 64 && @event.KeyChar <= 90 - 64)
							@char = string.Empty + @event.KeyChar;
						else if (@event.KeyChar == 32)
							@char = string.Empty + (char) (0);
					}
					else if ((@event.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt)
					{
						if (@event.KeyChar >= 65 && @event.KeyChar <= 90)
							@char = string.Empty + (char)(@event.KeyChar - 64);
					}
					if (@event.KeyChar != 0)
					{
						if ((@event.Modifiers & ConsoleModifiers.Control) != ConsoleModifiers.Control
							&& (@event.Modifiers & ConsoleModifiers.Alt) != ConsoleModifiers.Alt)
						{
							@char = string.Empty + @event.KeyChar;
						}
					}
					break;
			}

			if (!string.IsNullOrEmpty(@char))
			{
				//this.show_cursor(); //TODO; implement
				Array.ForEach(@char.ToArray(), c => termHandler(c));
			}
		}
	}
}