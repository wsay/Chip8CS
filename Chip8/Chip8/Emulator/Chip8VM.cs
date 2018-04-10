using System;
using System.Threading;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Chip8.Display;
using Chip8.Exceptions;

namespace Chip8.Emulator {

	/// <summary>
	/// Emulates a chip8 virtual machine
	/// </summary>
	class Chip8VM {

		//OpCode execution delegate - when opcode is identified
		//this is set to the appropriate function to execute that opcode
		private delegate void DelegateRunOpCOde(ushort opcode);
		private DelegateRunOpCOde RunOpCode;

		//Random number generator
		Random rand = new Random();

		#region Chip8 Constants
		private const int memory_size = 4096;
		private const int num_registers = 16;
		private const int graphics_width = 64;
		private const int graphics_height = 32;
		private const int stack_size = 16;
		#endregion

		#region Chip8 System memory
		//To store the current opcode
		private ushort opcode;

		//Main memory
		//0x000-0x1FF - Chip 8 interpreter(contains font set in emu)
		//0x050-0x0A0 - Used for the built in 4x5 pixel font set(0-F)
		//0x200-0xFFF - Program ROM and work RAM
		private byte[] memory;

		//CPU registers V0 .. V15 - V16 is the carry flag
		private byte[] V;

		//Index register
		private ushort I;

		//Program counter
		private ushort pc;

		//Graphics memory, 64x32 pixels (I will probably implement them as hashes on a console)
		private byte[] graphics;

		//These are two timers - when set above zero they will decrement at 60Hz
		//when the sound_timer reaches 0 from decrementing a buzzer sounds
		private byte delay_timer;
		private byte sound_timer;
		#endregion

		#region Class variables
		//Signifies when we want to update the screen
		//0x00E0 – Clears the screen
		//0xDXYN – Draws a sprite on the screen
		private bool drawFlag = false;

		//We will need a stack - and it'll be 16 levels deep
		//we could implement this in the 4kb of chip8 memory,
		//but that would make things more complex and we're not
		//going to lose sleep over a few bytes of memory on a 
		//modern system.
		private ushort[] stack = new ushort[stack_size];
		//this will work as our stack pointer
		private ushort stackpointer;
		//Display render system
		IDisplay display;

		//Stores a dictionary to map from .NET keycodes to
		//chip8 key hex codes
		readonly Dictionary<ConsoleKey, byte> keyToHexMap;

		//Stores a dictionary to map from chip8 key hex codes to
		//.NET keycodes
		readonly Dictionary<byte, ConsoleKey> hexToKeyMap;

		//Stores whether a key is being pressed, without
		//blocking
		ConsoleKeyInfo? keyPressed;

		//Continue the emulation loop (halted when we return from the main program)
		bool ContinueEmulationLoop = true;
		#endregion

		/// <summary>
		/// Constructor
		/// Initialises keymap
		/// </summary>
		public Chip8VM() {
			//Keypad Keyboard
			//+-+-+-+-+		+-+-+-+-+
			//|1|2|3|C|		|1|2|3|4|
			//+-+-+-+-+		+-+-+-+-+
			//|4|5|6|D|		|Q|W|E|R|
			//+-+-+-+-+	=>	+-+-+-+-+
			//|7|8|9|E|		|A|S|D|F|
			//+-+-+-+-+		+-+-+-+-+
			//|A|0|B|F|		|Z|X|C|V|
			//+-+-+-+-+		+-+-+-+-+

			keyToHexMap = new Dictionary<ConsoleKey, byte> {
				//Top row
				[ConsoleKey.D1] = 0x1,
				[ConsoleKey.D2] = 0x2,
				[ConsoleKey.D3] = 0x3,
				[ConsoleKey.D4] = 0xC,

				//Second row
				[ConsoleKey.Q] = 0x4,
				[ConsoleKey.W] = 0x5,
				[ConsoleKey.E] = 0x6,
				[ConsoleKey.R] = 0xD,

				//Third row
				[ConsoleKey.A] = 0x7,
				[ConsoleKey.S] = 0x8,
				[ConsoleKey.D] = 0x9,
				[ConsoleKey.F] = 0xE,

				//Fourth row
				[ConsoleKey.Z] = 0xA,
				[ConsoleKey.X] = 0x0,
				[ConsoleKey.C] = 0xB,
				[ConsoleKey.V] = 0xF
			};

			hexToKeyMap = new Dictionary<byte, ConsoleKey> {
				[0x1] = ConsoleKey.D1,
				[0x2] = ConsoleKey.D2,
				[0x3] = ConsoleKey.D3,
				[0xC] = ConsoleKey.D4,

				[0x4] = ConsoleKey.Q,
				[0x5] = ConsoleKey.W,
				[0x6] = ConsoleKey.E,
				[0xD] = ConsoleKey.R,

				[0x7] = ConsoleKey.A,
				[0x8] = ConsoleKey.S,
				[0x9] = ConsoleKey.D,
				[0xE] = ConsoleKey.F,

				[0xA] = ConsoleKey.Z,
				[0x0] = ConsoleKey.X,
				[0xB] = ConsoleKey.C,
				[0xF] = ConsoleKey.V
			};
		}

		/// <summary>
		/// Runs the main chip8 loop
		/// </summary>
		/// <param name="programName">The name of the program to run,
		/// from the included programs</param>
		public void MainLoop(string programName = "INVADERS") {
			// Instantiate render system
			display = new ConsoleDisplay();

			// Initialize the Chip8 system and load the game into the memory  
			Initialize(programName);

			// Emulation loop
			while (ContinueEmulationLoop) {
				//TODO: doesn't seem to work very well - need to improve this
                // as it affects the keyboard input (i.e. it barely works)
				Thread.Sleep(16); //Keep us running at roughly 60 hz
				keyPressed = TryGetKeyPress();

				//If user pressed escape, end loop
				if (keyPressed?.Key == ConsoleKey.Escape) {
					ContinueEmulationLoop = false;
					continue;
				}

				try {
					// Emulate one cycle
					EmulateCycle();
				} catch (ProgramCounterOutOfBoundsException e) {
					Console.Write($"Error: {e.Message}`n Restarting...");
					Thread.Sleep(2000);
					Initialize(programName); //re-initialise everything with the same program
				}

				// If the draw flag is set, update the screen
				if (drawFlag) {
					display.DrawDisplay(graphics, graphics_height, graphics_width);
				}
			}
		}

		/// <summary>
		/// Emualtes one cycle of the chip8 system
		/// </summary>
		private void EmulateCycle() {
			// Fetch Opcode

			//Opcode is two bytes stored in memory - we need to store them
			//into one two-byte variable

			//0xA2       0xA2 << 8 = 0xA200   HEX
			//10100010   1010001000000000     BIN
			//0xA2       0xA2 << 8 = 0xA200   HEX
			//10100010   1010001000000000     BIN
			if (pc + 1 < memory_size) {
				byte opc1 = memory[pc];
				byte opc2 = memory[pc + 1];
				opcode = (ushort)(opc1 << 8 | opc2);
			} else {
				throw new ProgramCounterOutOfBoundsException
					($"Program counter at {pc}, memory size is {memory_size}, cannot fetch 2-byte opcode as {pc + 1} is out of bounds.");
			}

			// Decode Opcode
			DecodeOpcode(opcode);

			// Execute Opcode
			RunOpCode?.Invoke(opcode);

			// Update timers
			UpdateTimers();
		}

		/// <summary>
		/// Decodes a given opcode and sets the RunOpCode delegate
		/// to the correct opcode function.
		/// </summary>
		/// <param name="opcode">16 bit opcode</param>
		private void DecodeOpcode(ushort opcode) {
			//This is gross - badly needs refactoring.
			switch (opcode & 0xF000) {
				case 0xA000:
					RunOpCode = Run0xANNN;
					break;

				case 0x1000:
					RunOpCode = Run0x1NNN;
					break;

				case 0x2000:
					RunOpCode = Run0x2NNN;
					break;

				case 0x3000:
					RunOpCode = Run0x3XNN;
					break;

				case 0x4000:
					RunOpCode = Run0x4XNN;
					break;

				case 0x5000:
					RunOpCode = Run0x5XY0;
					break;

				case 0x6000:
					RunOpCode = Run0x6XNN;
					break;

				case 0x7000:
					RunOpCode = Run0x7XNN;
					break;

				case 0x8000: //Op codes where first four bits are 0x8
					switch (opcode & 0x000F) {
						case 0x0000:
							RunOpCode = Run0x8XY0;
							break;

						case 0x0001:
							RunOpCode = Run0x8XY1;
							break;

						case 0x0002:
							RunOpCode = Run0x8XY2;
							break;

						case 0x0003:
							RunOpCode = Run0x8XY3;
							break;

						case 0x0004:
							RunOpCode = Run0x8XY4;
							break;

						case 0x0005:
							RunOpCode = Run0x8XY5;
							break;

						case 0x0006:
							RunOpCode = Run0x8XY6;
							break;

						case 0x0007:
							RunOpCode = Run0x8XY7;
							break;

						case 0x000E:
							RunOpCode = Run0x8XYE;
							break;

						default:
							throw new UnknownOpcodeException($"Unknown Opcode: 0x{opcode:x}");
					}
					break;

				case 0x9000:
					RunOpCode = Run0x9XY0;
					break;

				case 0xB000:
					RunOpCode = Run0xBNNN;
					break;

				case 0xC000:
					RunOpCode = Run0xCXNN;
					break;

				case 0xD000:
					RunOpCode = Run0xDXYN;
					break;

				case 0xE000:
					switch (opcode & 0x000F) {
						case 0x000E:
							RunOpCode = Run0xEX9E;
							break;
						case 0x0001:
							RunOpCode = Run0xEXA1;
							break;
						default:
							throw new UnknownOpcodeException($"Unknown Opcode: 0x{opcode:x}");
					}
					break;
				case 0xF000:
					switch (opcode & 0x00FF) {
						case 0x0007:
							RunOpCode = Run0xFX07;
							break;
						case 0x000A:
							RunOpCode = Run0xFX0A;
							break;
						case 0x0015:
							RunOpCode = Run0xFX15;
							break;
						case 0x0018:
							RunOpCode = Run0xFX18;
							break;
						case 0x001E:
							RunOpCode = Run0xFX1E;
							break;
						case 0x0029:
							RunOpCode = Run0xFX29;
							break;
						case 0x0055:
							RunOpCode = Run0xFX55;
							break;
						case 0x0065:
							RunOpCode = Run0xFX65;
							break;
						case 0x0033:
							RunOpCode = Run0xFX33;
							break;
						default:
							throw new UnknownOpcodeException($"Unknown Opcode: 0x{opcode:x}");
					}
					break;

				case 0x0000:
					switch (opcode & 0x00FF) {
						case 0x00E0:
							RunOpCode = Run0x00E0;
							break;

						case 0x00EE:
							RunOpCode = Run0x00EE;
							break;

						default:
							throw new UnknownOpcodeException($"Unknown Opcode: 0x{opcode:x}");
					}
					break;

				default:
					throw new UnknownOpcodeException($"Unknown Opcode: 0x{opcode:x}");
			}
		}

		/// <summary>
		/// Updates both the timers - if the sound timer reaches
		/// 0 in this function, tells the display class to beep.
		/// </summary>
		private void UpdateTimers() {
			if (delay_timer > 0) {
				delay_timer--;
			}
			if (sound_timer > 0) {
				sound_timer--;
				if (sound_timer == 0) {
					display.Beep();
				}
			}
		}

		/// <summary>
		/// Loads a program into memory
		/// </summary>
		/// <param name="programName">String identifying the program to load</param>
		private void LoadGame(string programName) {
			//Read bytes from file
			byte[] fileBytes = File.ReadAllBytes($".\\Resources\\{programName}");

			//Copy them into system memory
			for (int i = 0; i < fileBytes.Length; ++i) {
				if (i > memory.Length - 1) {
					throw new OutOfMemoryBoundsException($"Ran out of memory loading program {programName}");
				}
				memory[i + 0x200] = fileBytes[i];
			}
		}

		/// <summary>
		/// Clears the memory, registers, and screen
		/// </summary>
		/// <param name="programName">Program to load after initialised</param>
		private void Initialize(string programName) {
			drawFlag = false;

			I = 0;
			pc = 0x200; //Programs always start at 0x200
			delay_timer = 0;
			sound_timer = 0;
			stackpointer = 0;
			ContinueEmulationLoop = true;

			memory = new byte[memory_size];
			V = new byte[num_registers];
			graphics = new byte[graphics_width * graphics_height];
			stack = new ushort[stack_size];

			LoadFontset();

			LoadGame(programName);
		}

		/// <summary>
		/// Loads the Chip8 fontset into the correct location in
		/// system memory, from the static class it is stored in.
		/// </summary>
		private void LoadFontset() {
			//Load fontset into chip8 memory
			for (int i = 0; i < FontSet.Values.Length; ++i) {
				memory[i] = FontSet.Values[i];
			}
		}

		/// <summary>
		/// Checks whether keys have been pressed since last call,
		/// if they have, returns the last key pressed. If not, returns
		/// null.
		/// </summary>
		/// <returns>Last key pressed, or null if none </returns>
		private ConsoleKeyInfo? TryGetKeyPress() {
			//Use a while loop to clear extra key presses that may have queued up and only keep the last read
			ConsoleKeyInfo? keyPressed = null;
			while (Console.KeyAvailable) {
				keyPressed = Console.ReadKey(true);
			}
			return keyPressed;
		}

		/// <summary>
		/// Gets the X component of an opcode
		/// </summary>
		/// <param name="opcode">The full opcode</param>
		/// <returns>X component</returns>
		int GetY(ushort opcode) {
			return (opcode & 0b0000_0000_1111_0000) >> 4;
		}

		/// <summary>
		/// Gets the Y component of an opcode
		/// </summary>
		/// <param name="opcode">The full opcode</param>
		/// <returns>Y component</returns>
		int GetX(ushort opcode) {
			return (opcode & 0b0000_1111_0000_0000) >> 8;
		}

		/// <summary>
		/// Gets the X and Y component of an opcode
		/// </summary>
		/// <param name="opcode">The full opcode</param>
		/// <returns>X and Y component as tuple</returns>
		(int X, int Y) GetXY(ushort opcode) {
			return (GetX(opcode), GetY(opcode));
		}

		#region Opcode Functions
		/// <summary>
		/// Opcode: 0x00E0
		///	Clears screen
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x00E0(ushort opcode) {
			graphics = new byte[graphics_width * graphics_height];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x00EE
		/// Decrements the stack and returns to address on stack
		/// (End subroutine)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x00EE(ushort opcode) {
			if (stackpointer == 0) {
				ContinueEmulationLoop = false;
				return;
			}
			--stackpointer; //Decrement stackpointer
			pc = stack[stackpointer]; //Retrieve the last program position from the stack
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x1NNN
		/// Goto - jumps to address NNN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x1NNN(ushort opcode) {
			pc = (ushort)(opcode & 0b0000_1111_1111_1111); //jump to new indicated location
		}

		/// <summary>
		/// Opcode: 0x2NNN
		/// Increments stack and moves to instruction at NNN
		/// (Start subroutine)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x2NNN(ushort opcode) {
			if (stackpointer == (stack_size - 1)) {
				throw new StackOverflowException($"Requested operation 0x{opcode:x} at location {pc:x} caused a stack overflow.");
			}
			stack[stackpointer] = pc; //Store the current program counter in stack
			++stackpointer; //Increment stackpointer
			pc = (ushort)(opcode & 0b0000_1111_1111_1111); //jump to new indicated location
		}

		/// <summary>
		/// Opcode: 0x3XNN
		/// Skip next instruction if VX == NN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x3XNN(ushort opcode) {
			int X = GetX(opcode);

			if (V[X] == (opcode & 0b0000_0000_1111_1111)) {
				pc += 4; // Skip over the next opcode
			} else {
				pc += 2;
			}
		}

		/// <summary>
		/// Opcode: 0x4XNN
		/// Skip next instruction if VX != NN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x4XNN(ushort opcode) {
			int X = GetX(opcode);

			if (V[X] != (opcode & 0b0000_0000_1111_1111)) {
				pc += 4; // Skip over the next opcode
			} else {
				pc += 2;
			}
		}

		/// <summary>
		/// Opcode: 0x5XY0
		/// Skip next instruction if VX == VY
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x5XY0(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			if (V[X] == V[Y]) {
				pc += 4; // Skip over the next opcode
			} else {
				pc += 2;
			}
		}

		/// <summary>
		/// Opcode: 0x6XNN
		///  Sets VX to NN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x6XNN(ushort opcode) {
			int X = GetX(opcode);

			V[X] = (byte)(opcode & 0b0000_0000_1111_1111);
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x7XNN
		///  Adds NN to VX without modifying carry flag
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x7XNN(ushort opcode) {
			int X = GetX(opcode);

			V[X] += (byte)(opcode & 0b0000_0000_1111_1111);
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY0
		/// Sets VX to VY
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY0(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[X] = V[Y];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY1
		/// Sets VX to VX or VY (bitwise)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY1(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[X] |= V[Y];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY2
		/// Sets VX to VX and VY (bitwise)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY2(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[X] &= V[Y];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY3
		/// Sets VX to VX xor VY (bitwise)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY3(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[X] ^= V[Y];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY4
		/// Adds the value of VY to VX. Register VF is set to 1
		/// when there is a carry and set to 0 when there isn’t. 
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY4(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			if (V[Y] < (byte.MaxValue - V[X])) {
				V[0xF] = 1; //carry
			} else {
				V[0xF] = 0;
			}

			V[X] += V[Y];
			pc += 2;

		}

		/// <summary>
		/// Opcode: 0x8XY5
		/// VY is subtracted from VX. If there's a borrow, VF is set to 0,
		/// if there isn't VF is set to 1.
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY5(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			if (V[Y] < (byte.MaxValue - V[X])) {
				V[0xF] = 1;
			} else {
				V[0xF] = 0;
			}

			V[X] -= V[Y];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY6
		/// Shifts VY right by 1 and copies the result to VX.
		/// VF is set to the value of the least significant bit
		/// of VY before the shift.
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY6(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[0xF] = (byte)(V[Y] & 0b0000_0000_0000_0001);
			V[X] = V[Y] = (byte)(V[Y] >> 1);

			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XY7
		/// VX is subtracted from VY. If there's a borrow, VF is set to 0,
		/// if there isn't VF is set to 1.
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XY7(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			if (V[Y] < (0xFF - V[X])) {
				V[0xF] = 1;
			} else {
				V[0xF] = 0;
			}

			V[Y] -= V[X];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x8XYE
		/// Shift VY left by one and copies the result to VX,
		/// VF is set to the most significant bit of VY before shift
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x8XYE(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			V[0xF] = (byte)(V[Y] & 0b1000_0000_0000_0000);
			V[X] = V[Y] = (byte)(V[Y] << 1);

			pc += 2;
		}

		/// <summary>
		/// Opcode: 0x9XY0
		/// Skips the next instruction if VX != VY
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0x9XY0(ushort opcode) {
			var (X, Y) = GetXY(opcode);

			if (V[X] != V[Y]) {
				pc += 4; //skip instruction
			} else {
				pc += 2; // don't skip
			}
		}


		/// <summary>
		/// Opcode: 0xBNNN
		/// Jumps to the address NNN + V0
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xBNNN(ushort opcode) {
			pc = (ushort)(V[0] + (opcode & 0b0000_1111_1111_1111));
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xCXNN
		/// Sets VX to the result of a bitwise and with a random number (0-255) and NN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xCXNN(ushort opcode) {
			int X = GetX(opcode);

			//Get random number
			byte[] randomByte = new byte[1];
			rand.NextBytes(randomByte);

			V[X] = (byte)(randomByte[0] & (opcode & 0b0000_0000_1111_1111));
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xDXYN
		/// Draws a sprite at coordinate (VX, VY) that has a width of 8 pixels and a height of N pixels.
		/// Each row of 8 pixels is read as bit-coded starting from memory location I;
		/// I value doesn’t change after the execution of this instruction.
		/// VF is set to 1 if any screen pixels are flipped from set to unset when
		/// the sprite is drawn, and to 0 if that doesn’t happen
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xDXYN(ushort opcode) {
			//Re-implemented - restructured array
			var (X, Y) = GetXY(opcode);
			ushort height = (ushort)(opcode & 0b0000_0000_0000_1111);
			ushort pixel;

			V[0xF] = 0;

			for (int yline = 0; yline < height; yline++) {
				pixel = memory[I + yline];
				for (int xline = 0; xline < 8; xline++) {
					if ((pixel & (0b1000_0000 >> xline)) != 0) {
						if (graphics[(V[X] + xline + ((V[Y] + yline) * graphics_width))] == 1) {
							V[0xF] = 1;
						}
						graphics[V[X] + xline + ((V[Y] + yline) * graphics_width)] ^= 1;
					}
				}
			}

			drawFlag = true;

			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xEXA1
		/// IF the key stored in VX isn't pressed, skip next
		/// instruction
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xEXA1(ushort opcode) {
			int X = GetX(opcode);

			if (keyPressed?.Key != hexToKeyMap[V[X]]) {
				pc += 4;
				return;
			}

			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xEX9E
		/// If the key stored in VX is pressed, skip next
		/// instruction
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xEX9E(ushort opcode) {
			int X = GetX(opcode);

			if (keyPressed?.Key == hexToKeyMap[V[X]]) {
				pc += 4;
				return;
			}

			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xANNN
		/// Which sets I to address NNN
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xANNN(ushort opcode) {
			I = (ushort)(opcode & 0b0000_1111_1111_1111);
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX07
		/// Sets VX to the value of the delay timer
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX07(ushort opcode) {
			int X = GetX(opcode);
			V[X] = delay_timer;
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX0A
		/// A key press is awaited, and then stored in VX.
		/// (Blocking Operation. All instruction halted until next key event)
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX0A(ushort opcode) {
			int X = GetX(opcode);
			ConsoleKeyInfo key;

			do {
				key = Console.ReadKey();
			} while (!keyToHexMap.ContainsKey(key.Key));


			V[X] = keyToHexMap[key.Key];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX15
		/// Sets the delay times to VX
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX15(ushort opcode) {
			int X = GetX(opcode);
			delay_timer = V[X];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX18
		/// Sets the delay times to VX
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX18(ushort opcode) {
			int X = GetX(opcode);
			sound_timer = V[X];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX1E
		/// Adds VX to I
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX1E(ushort opcode) {
			int X = GetX(opcode);
			I += V[X];
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX29
		/// Sets I to the location of the sprite for the character in VX
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX29(ushort opcode) {
			int X = GetX(opcode);
			I = (ushort)(V[X] * 0x5);
			pc += 2;
		}


		/// <summary>
		/// Opcode: 0xFX55
		/// Stores V0 to VX (including VX) in memory starting at address I.
		/// I is increased by 1 for each value written.
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX55(ushort opcode) {
			int X = GetX(opcode);

			for (int j = 0; j < X; ++j) {
				memory[I] = V[j];
				++I;
			}
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX65
		/// Fills V0 to VX (including VX) with values from memory starting at address I.
		/// I is increased by 1 for each value written.
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX65(ushort opcode) {
			int X = GetX(opcode);
			for (int j = 0; j < X; ++j) {
				V[j] = memory[I];
				++I;
			}
			pc += 2;
		}

		/// <summary>
		/// Opcode: 0xFX33
		/// Stores the Binary-coded decimal representation of VX at
		/// the addresses I, I plus 1, and I plus 2
		/// </summary>
		/// <param name="opcode"></param>
		private void Run0xFX33(ushort opcode) {
			int X = GetX(opcode);

			memory[I] = (byte)(V[X] / 100);
			memory[I + 1] = (byte)((V[X] / 10) % 10);
			memory[I + 2] = (byte)((V[X] % 100) % 10);
			pc += 2;
		}
		#endregion


	}
}
