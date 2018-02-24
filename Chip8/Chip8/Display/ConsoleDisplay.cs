using System;

namespace Chip8.Display
{
	/// <summary>
	/// A console based backend for the IDisplay interface
	/// </summary>
    class ConsoleDisplay : IDisplay
    {
		//TODO: Need a better display that's less slow. Maybe a WPF window with a bitmap, based on the examples I gave students?
		bool beep = false;

		/// <summary>
		/// Draws a square array of bytes to the screen, using spaces and hashes
		/// </summary>
		/// <param name="graphics">A square array where 1 = #, 0 = ' ' </param>
		public void DrawDisplay(byte[] graphics, int height, int width) {
			Console.CursorVisible = false;
			//Console.Clear();
			for(int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					if (graphics[y * width + x] == 1) {
						Console.SetCursorPosition(x, y);
						Console.Write('#');
					} else {
						Console.SetCursorPosition(x, y);
						Console.Write(' ');
					}
				}
			}

			if (beep) {
				Console.Write("BEEP!!");
				beep = false;
			}
			Console.SetCursorPosition(0, 0);
		}

		/// <summary>
		/// Print beep to the screen
		/// </summary>
		public void Beep() {
			beep = true;
		}
	}
}
