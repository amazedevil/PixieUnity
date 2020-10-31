using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Pixie.Unity
{
    public abstract class PXProtocolBase : MonoBehaviour
    {
        public abstract void SetupStream(Stream stream);

        public abstract Task StartReading();

        public abstract void Initialize(IPXProtocolContact contact);

        public abstract Task SendMessage(byte[] message);

        public abstract Task SendResponse(ushort id, byte[] response);

        public abstract Task<byte[]> SendRequestMessage(byte[] message);

        public abstract PXProtocolState GetState();

        public abstract void Dispose();
    }
}
