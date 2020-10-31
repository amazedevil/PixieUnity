using System;
using System.Collections.Generic;
using System.Text;

namespace Pixie.Unity
{
    public class PXConnectionClosedException : Exception
    {
        public PXConnectionClosedException() : base("Connection closed") { }
    }
}
