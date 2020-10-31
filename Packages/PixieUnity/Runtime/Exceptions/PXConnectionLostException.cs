using System;
using System.Collections.Generic;
using System.Text;

namespace Pixie.Unity
{
    public class PXConnectionLostException : Exception
    {
        public PXConnectionLostException() : base("Connection lost") { }
    }
}
