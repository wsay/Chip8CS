using System;
using System.Runtime.Serialization;

namespace Chip8.Exceptions
{
    class UnknownOpcodeException: Exception
    {
		public UnknownOpcodeException() : base() {

		}

		public UnknownOpcodeException(string message) : base(message) {

		}

		public UnknownOpcodeException(string message, Exception inner) : base(message, inner) {

		}

		public UnknownOpcodeException(SerializationInfo info, StreamingContext context) : base(info, context) {

		}
    }
}
