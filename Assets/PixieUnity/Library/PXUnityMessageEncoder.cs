using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Pixie.Unity
{
    class PXUnityMessageEncoder
    {
        private Dictionary<int, Type> messageTypes;

        public PXUnityMessageEncoder(IEnumerable<Type> messageTypes) {
            this.messageTypes = new Dictionary<int, Type>();
            foreach (var t in messageTypes) {
                this.messageTypes[PXUnityMessageInfo.GetMessageTypeHashCode(t)] = t;
            }
        }

        public object DecodeMessage(byte[] data) {
            var obj = JObject.Parse(Encoding.UTF8.GetString(data));
            var hash = obj[PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_NAME].Value<int>();

            if (!messageTypes.ContainsKey(hash)) {
                throw new Exception($"Unregistered message with hash {hash} received");
            }

            return (obj[PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_BODY] as JObject).ToObject(messageTypes[hash]);
        }

        public byte[] EncodeMessage(object message) {
            JObject obj = JObject.FromObject(new Dictionary<string, object> {
                { PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_NAME, PXUnityMessageInfo.GetMessageTypeHashCode(message.GetType()) },
                { PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_BODY, message },
            });

            string objAsString = obj.ToString(Newtonsoft.Json.Formatting.None);

            byte[] buffer = new byte[Encoding.UTF8.GetByteCount(objAsString)];
            Encoding.UTF8.GetBytes(objAsString, 0, objAsString.Length, buffer, 0);

            return buffer;
        }
    }
}