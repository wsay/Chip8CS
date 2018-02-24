namespace Chip8.Display
{
	/// <summary>
	/// Defines an interface for display backends
	/// </summary>
    interface IDisplay
    {
		/// <summary>
		/// Should draw a square array of bytes to the screen, using whatever
		/// backend has been instantiated.
		/// </summary>
		/// <param name="graphics">Byte array to draw to screen</param>
		/// <param name="height">Height of the output display</param>
		/// <param name="width">Width of teh output display</param>
		void DrawDisplay(byte[] graphics, int height, int width);

		/// <summary>
		/// Beep, or provide some user feedback to let them know
		/// that a beep has happened.
		/// </summary>
		void Beep();
	}
}
