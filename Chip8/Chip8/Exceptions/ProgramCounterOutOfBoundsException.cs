using System;
using System.Runtime.Serialization;

namespace Chip8.Exceptions
{
    class ProgramCounterOutOfBoundsException : Exception
    {
		public ProgramCounterOutOfBoundsException() : base() {

		}

		public ProgramCounterOutOfBoundsException(string message) : base(message) {

		}

		public ProgramCounterOutOfBoundsException(string message, Exception inner) : base(message, inner) {

		}

		public ProgramCounterOutOfBoundsException(SerializationInfo info, StreamingContext context) : base(info, context) {

		}
    }
}
