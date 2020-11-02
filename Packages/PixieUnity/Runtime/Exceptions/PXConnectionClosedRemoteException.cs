using System;
using System.Collections.Generic;
using System.Text;

namespace Pixie.Unity
{
    public class PXConnectionClosedRemoteException : Exception
    {
        public PXConnectionClosedRemoteException() : base("Connection closed remotely") { }
    }
}
