using System;
using System.Text;
using System.IO;

namespace Chip8.Display
{
	/// <summary>
	/// A console based backend for the IDisplay interface
	/// </summary>
    class ConsoleDisplay : IDisplay
    {
		//TODO: Need a better display that's less slow. Maybe a WPF window with a bitmap, based on the examples I gave students?
		bool beep = false;

        StringBuilder stringBuilder = new StringBuilder();
        string cachedScreen;
		/// <summary>
		/// Draws a square array of bytes to the screen, using spaces and hashes
		/// </summary>
		/// <param name="graphics">A square array where 1 = #, 0 = ' ' </param>
		public void DrawDisplay(byte[] graphics, int height, int width) {
			Console.CursorVisible = false;
            stringBuilder.Clear();

            for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					if (graphics[y * width + x] == 1) {
                        stringBuilder.Append('#');
					} else {
                        stringBuilder.Append(' ');
					}
				}
                stringBuilder.Append('\n');
			}

            if (stringBuilder.ToString() != cachedScreen) //Only update if screen has changed
            {
                string localString = stringBuilder.ToString();
                Console.Write(localString);
                cachedScreen = localString;
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
