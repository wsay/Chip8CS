using System;
using System.Runtime.Serialization;

namespace Chip8.Exceptions
{
    class OutOfMemoryBoundsException : Exception
    {
		public OutOfMemoryBoundsException() : base() {

		}

		public OutOfMemoryBoundsException(string message) : base(message) {

		}

		public OutOfMemoryBoundsException(string message, Exception inner) : base(message, inner) {

		}

		public OutOfMemoryBoundsException(SerializationInfo info, StreamingContext context) : base(info, context) {

		}
    }
}
