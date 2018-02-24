using System;
using System.Runtime.Serialization;

namespace Chip8.Exceptions
{
    class ProgramLoadingException : Exception
    {
		public ProgramLoadingException() : base() {

		}

		public ProgramLoadingException(string message) : base(message) {

		}

		public ProgramLoadingException(string message, Exception inner) : base(message, inner) {

		}

		public ProgramLoadingException(SerializationInfo info, StreamingContext context) : base(info, context) {

		}
	}
}
