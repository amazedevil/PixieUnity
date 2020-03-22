using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixie.Unity
{
    public interface IPXProtocolContact
    {
        void RequestReconnect();

        void ReceivedMessage(byte[] message);

        void ClientDisconnected();

        void ClientException(Exception e);
    }
}
