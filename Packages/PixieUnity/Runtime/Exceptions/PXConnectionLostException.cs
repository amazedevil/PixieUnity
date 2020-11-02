using System;
using System.IO;

namespace Pixie.Unity
{
    public class PXConnectionLostException : Exception
    {
        public Stream ConnectionStream { get; private set; }

        public PXConnectionLostException(Stream s) : base("Connection lost") {
            ConnectionStream = s;
        }
    }
}
