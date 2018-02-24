using System;

using Chip8.Emulator;

namespace Chip8
{
    class Program
    {
        static void Main(string[] args)
        {
			Chip8VM c8 = new Chip8VM();
			c8.MainLoop();
        }
    }
}
