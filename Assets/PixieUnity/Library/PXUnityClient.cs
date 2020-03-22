using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using UnityEngine;
using System.Linq;

namespace Pixie.Unity
{
    public class PXUnityClient : MonoBehaviour, IPXProtocolContact
    {
        private const int RECONNECTION_DELAY = 5000;

        [SerializeField]
        private string serverHost = "localhost";

        [SerializeField]
        private int serverPort = 7777;

        [SerializeField]
        private bool autoSearchEventHandlers = true;

        [SerializeField]
        private GameObject[] eventKeepingGameObjects;

        [SerializeField]
        private PXProtocolBase protocol = null;

        private TcpClient socketConnection = null;
        private PXUnityMessageEncoder encoder = null;
        private PXUnityMessageHandlerRawBase[] handlers;
        private ConcurrentQueue<Action> mainThreadActionQueue = new ConcurrentQueue<Action>();

        private void Awake() {
            if (protocol == null) {
                protocol = this.gameObject.AddComponent<PXReliableDeliveryProtocol>();
            }

            if (autoSearchEventHandlers) {
                handlers = UnityEngine.Object.FindObjectsOfType<PXUnityMessageHandlerRawBase>();
            } else {
                if (eventKeepingGameObjects.Length == 0) {
                    eventKeepingGameObjects = new GameObject[] { this.gameObject };
                }

                handlers = eventKeepingGameObjects
                    .Select(ek => ek.GetComponents<PXUnityMessageHandlerRawBase>())
                    .SelectMany(mh => mh)
                    .ToArray();
            }

            this.encoder = new PXUnityMessageEncoder(handlers.Select(x => x.DataType).ToArray());
            this.protocol.Initialize(this);

            StartCoroutine(StartDataStreamPreparing());
        }

        private void FixedUpdate() {
            while (mainThreadActionQueue.TryDequeue(out Action action)) {
                action();
            }
        }

        private void OnDestroy() {
            FinalizeDataStream();
        }

        private IEnumerator StartDataStreamPreparing() {
            while (!PrepareDataStream()) {
                yield return new WaitForSeconds(RECONNECTION_DELAY);
            }
        }

        private bool PrepareDataStream() {
            try {
                socketConnection = new TcpClient(serverHost, serverPort);
                protocol.SetupStreams(socketConnection.GetStream());

                return true;
            } catch (Exception e) {
                FinalizeDataStream();
                Debug.LogError(e);
            }

            return false;
        }

        private void FinalizeDataStream() {
            socketConnection?.Close();
            socketConnection = null;
        }

        public void SendMessage(object message) {
            this.protocol.SendMessage(encoder.EncodeMessage(message));
        }

        private void ProcessCommand(object message) {
            var messageType = message.GetType();

            Debug.Log("Message received: " + messageType.ToString());

            foreach (var eh in handlers) {
                if (eh.DataType == messageType) {
                    eh.SetupData(message).Execute();
                }
            }
        }

        public void RequestReconnect() {
            FinalizeDataStream();
            mainThreadActionQueue.Enqueue(delegate {
                StartCoroutine(StartDataStreamPreparing());
            });
        }

        public void ReceivedMessage(byte[] message) {
            var decoded = this.encoder.DecodeMessage(message);
            mainThreadActionQueue.Enqueue(delegate {
                ProcessCommand(decoded);
            });
        }

        public void ClientDisconnected() {
            //do nothing
        }

        public void ClientException(Exception e) {
            mainThreadActionQueue.Enqueue(delegate {
                Debug.LogError(e);
            });
        }
    }
}