using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixie.Unity
{
    public interface IPXProtocolContact
    {
        void ReceivedMessage(byte[] message);

        void ReceivedRequestMessage(ushort id, byte[] message);

        void OnProtocolStateChanged();
    }
}
