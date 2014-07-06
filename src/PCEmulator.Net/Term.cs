using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class Term : IDisposable
	{
		private readonly int height;
		private readonly Action<char> termHandler;
		private int width;

		private int row;
		private int column;

		private int[][] lines;

		private bool started;
		private readonly int defaultWidth;

		private int state;

		private int y_base;
		private long cur_attr;
		private int y_disp;
		private bool convert_lf_to_crlf;
		private List<int> esc_params;
		private int cur_param;
		private string output_queue;
		private const int def_attr = (7 << 3) | 0;

		private ConsoleColor[] fgColors = { ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Blue, ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.White };
		private ConsoleColor[] bgColors = { ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Blue, ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.White };
		private int cur_h;
		private int tot_h = 1000;

		public Term(int cols, int rows, Action<char> termHandler)
		{
			height = rows;
			defaultWidth = cols;
			this.termHandler = termHandler;
			cur_attr = def_attr;
			cur_h = height;

			Reset();
			BeginReadFromConsole();
		}

		public void Write(char @char)
		{
			var ka = 0;
			var la = 0;
			var va = new Action<int>(y =>
			{
				ka = Math.Min(ka, y);
				la = Math.Max(la, y);
			});

			var wa = new Action<Term, int, int>((s, x, y) =>
			{
				var ta = s.y_base + y;
				if (ta >= s.cur_h)
					ta -= s.cur_h;
				var l = s.lines[ta];
				var c = 32 | (def_attr << 16);
				for (var i = x; i < s.width; i++)
					l[i] = c;
				va(y);
			});

			var xa = new Action<Term, int[]>((s, ya) =>
			{
				if (ya.Length == 0)
				{
					s.cur_attr = def_attr;
				}
				else
				{
					for (var j = 0; j < ya.Length; j++)
					{
						var n = ya[j];
						if (n >= 30 && n <= 37)
						{
							s.cur_attr = (s.cur_attr & ~(7 << 3)) | ((n - 30) << 3);
						}
						else if (n >= 40 && n <= 47)
						{
							s.cur_attr = (s.cur_attr & ~7) | (n - 40);
						}
						else if (n == 0)
						{
							s.cur_attr = def_attr;
						}
					}
				}
			});

			const int za = 0;
			const int Aa = 1;
			const int Ba = 2;
			ka = this.height;
			la = -1;
			va(this.row);
			if (this.y_base != this.y_disp)
			{
				this.y_disp = this.y_base;
				ka = 0;
				la = this.height - 1;
			}
			//for (var i = 0; i < @char.length; i++)
			{
				var c = @char;//.charCodeAt(i);
				switch (this.state)
				{
					case za:
						switch ((ushort)c)
						{
							case 10:
								if (this.convert_lf_to_crlf)
								{
									this.column = 0;
								}
								this.row++;
								if (this.row >= this.height)
								{
									this.row--;
									this.scroll();
									ka = 0;
									la = this.height - 1;
								}
								break;
							case 13:
								this.column = 0;
								break;
							case 8:
								if (this.column > 0)
								{
									this.column--;
								}
								break;
							case 9:
								var n = (this.column + 8) & ~7;
								if (n <= this.width)
								{
									this.column = n;
								}
								break;
							case 27:
								this.state = Aa;
								break;
							default:
								if (c >= 32)
								{
									if (this.column >= this.width)
									{
										this.column = 0;
										this.row++;
										if (this.row >= this.height)
										{
											this.row--;
											this.scroll();
											ka = 0;
											la = this.height - 1;
										}
									}
									var ta = this.row + this.y_base;
									if (ta >= this.cur_h)
										ta -= this.cur_h;
									this.lines[ta][this.column] = (int) ((c & 0xffff) | (cur_attr << 16));
									this.column++;
									va(this.row);
								}
								break;
						}
						break;
					case Aa:
						if (c == 91)
						{
							this.esc_params = new List<int>();
							this.cur_param = 0;
							this.state = Ba;
						}
						else
						{
							this.state = za;
						}
						break;
					case Ba:
						if (c >= 48 && c <= 57)
						{
							this.cur_param = this.cur_param * 10 + c - 48;
						}
						else
						{
							this.esc_params.Add(this.cur_param);
							this.cur_param = 0;
							if (c == 59)
								break;
							this.state = za;
							switch ((int)c)
							{
								case 65:
									var n = this.esc_params[0];
									if (n < 1)
										n = 1;
									this.row -= n;
									if (this.row < 0)
										this.row = 0;
									break;
								case 66:
									n = this.esc_params[0];
									if (n < 1)
										n = 1;
									this.row += n;
									if (this.row >= this.height)
										this.row = this.height - 1;
									break;
								case 67:
									n = this.esc_params[0];
									if (n < 1)
										n = 1;
									this.column += n;
									if (this.column >= this.width - 1)
										this.column = this.width - 1;
									break;
								case 68:
									n = this.esc_params[0];
									if (n < 1)
										n = 1;
									this.column -= n;
									if (this.column < 0)
										this.column = 0;
									break;
								case 72:
									{
										int Ca;
										var ta = this.esc_params[0] - 1;
										if (this.esc_params.Count >= 2)
											Ca = this.esc_params[1] - 1;
										else
											Ca = 0;
										if (ta < 0)
											ta = 0;
										else if (ta >= this.height)
											ta = this.height - 1;
										if (Ca < 0)
											Ca = 0;
										else if (Ca >= this.width)
											Ca = this.width - 1;
										this.column = Ca;
										this.row = ta;
									}
									break;
								case 74:
									wa(this, this.column, this.row);
									for (var j = this.row + 1; j < this.height; j++)
										wa(this, 0, j);
									break;
								case 75:
									wa(this, this.column, this.row);
									break;
								case 109:
									xa(this, this.esc_params.ToArray());
									break;
								case 110:
									this.queue_chars("\x1b[" + (this.row + 1) + ";" + (this.column + 1) + "R");
									break;
								default:
									break;
							}
						}
						break;
				}
			}
			va(this.row);
			if (la >= ka)
				this.Refresh(ka, la);
		}

		private void queue_chars(string s)
		{
			this.output_queue += s;
			if (!string.IsNullOrEmpty(this.output_queue))
				JsEmu.SetTimeout(this.outputHandler, 0);
		}

		private void outputHandler()
		{
			if (string.IsNullOrEmpty(output_queue))
				return;
			foreach (var c in this.output_queue)
				this.termHandler(c);
			this.output_queue = "";
		}

		private void scroll()
		{
			if (cur_h < tot_h)
			{
				cur_h++;
			}
			if (++y_base == cur_h)
				y_base = 0;
			y_disp = y_base;
			var c = 32 | (def_attr << 16);
			var ia = new int[width];
			for (var x = 0; x < width; x++)
				ia[x] = c;
			var ta = y_base + height - 1;
			if (ta >= cur_h)
				ta -= cur_h;

			var newLines = new int[cur_h][];
			Array.Copy(lines, newLines, lines.Length);
			Console.MoveBufferArea(0, 1, Console.WindowWidth, Console.WindowHeight - 1, 0, 0);
			lines = newLines;
			lines[ta] = ia;
		}

		public void Dispose()
		{
			started = false;
		}

		private void Reset()
		{
			width = defaultWidth;
			row = 0;
			column = 0;
			ResetBufferAndWindows();
		}

		private void ResetBuffer()
		{
			UpdateLines();
		}

		private void ResetBufferAndWindows()
		{
			ResetBuffer();
			Console.ResetColor();
			//+1 to avoid automatic scroll
			Console.SetWindowSize(width+1, height);
			Console.SetBufferSize(width+1, height);
		}

		private void Refresh(int ka, int la)
		{
			int y;
			for (y = ka; y <= la; y++)
			{
				RefreshLine(y);
			}
		}

		private void RefreshLine(int y)
		{
			var ta = y + y_disp;
			if (ta >= cur_h)
				ta -= cur_h;
			var ia = lines[ta];
//				var na = "";
			var naa = new List<ConsoleItem>();
			var w = width;
			var oa = -1;
			var qa = def_attr;
			int i;
			var taa = new ConsoleItem();
			for (i = 0; i < w; i++)
			{
				qa = RefreshOne(ia, i, oa, qa, naa, ref taa);
			}
			if (qa != def_attr)
			{
				//na += "</span>";
				naa.Add(taa);
			}
			naa.Add(taa);
			Console.SetCursorPosition(0, y);
			foreach (var aa in naa)
			{
				Render(aa);
			}
		}

		private static void Render(ConsoleItem aa)
		{
			if (aa.c == null)
				return;

			Console.ForegroundColor = aa.fc;
			Console.BackgroundColor = aa.bc;
			Console.Write(aa.c);
		}

		private int RefreshOne(int[] ia, int i, int oa, int qa, List<ConsoleItem> naa, ref ConsoleItem taa)
		{
			var c = ia[i];
			var pa = (int) c >> 16;
			c &= 0xffff;
			if (i == oa)
			{
				pa = -1;
			}
			if (pa != qa)
			{
				if (qa != def_attr)
				{
//							na += "</span>";
					naa.Add(taa);
					taa = new ConsoleItem(); //if something without span, null will be filtered out
				}
				if (pa != def_attr)
				{
					if (pa == -1)
					{
//								na += "<span class=\"termReverse\">";
						naa.Add(taa); //if something without span, null will be filtered out
						taa = new ConsoleItem
						{
							fc = ConsoleColor.Black,
							bc = ConsoleColor.Green
						};
					}
					else
					{
//								na += "<span style=\"";
						naa.Add(taa); //if something without span, null will be filtered out
						taa = new ConsoleItem();
						var ra = (pa >> 3) & 7;
						var sa = pa & 7;
						if (ra != 7)
						{
//									na += "color:" + fg_colors[ra] + ';';
							taa.fc = fgColors[ra];
						}
						if (sa != 0)
						{
//									na += "background-color:" + bg_colors[sa] + ';';
							taa.bc = bgColors[sa];
						}
//								na += "\">";
					}
				}
			}
			switch (c)
			{
				case 32:
//							na += "&nbsp;";
					taa.c += " ";
					break;
				case 38:
//							na += "&amp;";
					taa.c += "&";
					break;
				case 60:
//							na += "&lt;";
					taa.c += "<";
					break;
				case 62:
//							na += "&gt;";
					taa.c += ">";
					break;
				default:
					if (c < 32)
					{
						//na += "&nbsp;";
						taa.c += " ";
					}
					else
					{
						//na += (char) c;
						taa.c += (char) c;
					}
					break;
			}
			qa = pa;
			return qa;
		}

		private void UpdateLines()
		{
			lines = new int[height][];
			var c = 32 | (def_attr << 16);
			for (var y = 0; y < cur_h; y++)
			{
				var ia = new int[width];
				for (var i = 0; i < width; i++)
					ia[i] = c;
				lines[y] = ia;
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
						this.scroll_disp(-1);
					else
						@char = "\x1b[A";
					break;
				case ConsoleKey.DownArrow:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						this.scroll_disp(1);
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
						this.scroll_disp(-(this.height - 1));
					else
						@char = "\x1b[5~";
					break;
				case ConsoleKey.PageDown:
					if ((@event.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
						this.scroll_disp(this.height - 1);
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

		private void scroll_disp(int n)
		{
			if (n >= 0)
			{
				for (var i = 0; i < n; i++)
				{
					if (this.y_disp == this.y_base)
						break;
					if (++this.y_disp == this.cur_h)
						this.y_disp = 0;
				}
			}
			else
			{
				n = -n;
				var ta = this.y_base + this.height;
				if (ta >= this.cur_h)
					ta -= this.cur_h;
				for (var i = 0; i < n; i++)
				{
					if (this.y_disp == ta)
						break;
					if (--this.y_disp < 0)
						this.y_disp = this.cur_h - 1;
				}
			}
			this.Refresh(0, this.height - 1);
		}

		private class ConsoleItem
		{
			public ConsoleColor fc = ConsoleColor.Gray;
			public ConsoleColor bc = ConsoleColor.Black;
			public string c;
		}
	}
}