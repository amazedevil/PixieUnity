using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Pixie.Unity
{
    public class PXUnityMessageWriter
    {
        private NetworkStream stream;

        public PXUnityMessageWriter(NetworkStream stream) {
            this.stream = stream;
        }

        public void Send(object message) {
            JObject obj = JObject.FromObject(new Dictionary<string, object>() {
            { PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_NAME, PXUnityMessageInfo.GetMessageTypeHashCode(message.GetType()) },
            { PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_BODY, message }
        });

            string objAsString = obj.ToString(Newtonsoft.Json.Formatting.None);

            byte[] buffer = new byte[Encoding.UTF8.GetByteCount(objAsString) + 1];
            Encoding.UTF8.GetBytes(objAsString, 0, objAsString.Length, buffer, 0);
            buffer[objAsString.Length] = 0;

            stream.Write(buffer, 0, buffer.Length);
        }
    }
}