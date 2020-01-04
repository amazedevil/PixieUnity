using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Pixie.Unity
{
    class PXUnityMessageReader
    {
        private NetworkStream stream;
        private List<byte> accumulator = new List<byte>();
        private Queue<object> messages = new Queue<object>();
        private byte[] buffer = new byte[128];
        private Dictionary<int, Type> messageTypes;

        public bool HasMessage
        {
            get { return messages.Count > 0; }
        }

        public PXUnityMessageReader(NetworkStream stream, Type[] messageTypes) {
            this.stream = stream;

            this.messageTypes = new Dictionary<int, Type>();
            foreach (var t in messageTypes) {
                this.messageTypes[PXUnityMessageInfo.GetMessageTypeHashCode(t)] = t;
            }
        }

        public void Update() {
            if (!this.stream.DataAvailable) {
                return;
            }

            var readCount = this.stream.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < readCount; i++) {
                if (buffer[i] == 0) {
                    MessageFinished();
                } else {
                    accumulator.Add(buffer[i]);
                }
            }
        }

        public object DequeueMessage() {
            return messages.Dequeue();
        }

        private void MessageFinished() {
            var obj = JObject.Parse(Encoding.UTF8.GetString(this.accumulator.ToArray()));

            messages.Enqueue(CreateMessage(
                obj[PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_NAME].Value<int>(),
                obj[PXUnityMessageInfo.MESSAGE_SERIALIZATION_FIELD_BODY] as JToken
            ));

            accumulator.Clear();
        }

        private object CreateMessage(int hash, JToken body) {
            return body.ToObject(this.messageTypes[hash]);
        }
    }
}