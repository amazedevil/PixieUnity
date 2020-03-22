using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Pixie.Unity
{
    public abstract class PXProtocolBase : MonoBehaviour
    {
        public abstract void SetupStreams(Stream stream);

        public abstract void Initialize(IPXProtocolContact contact);

        public abstract void SendMessage(byte[] message);
    }
}
