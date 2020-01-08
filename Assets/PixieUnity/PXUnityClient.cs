using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using UnityEngine;
using System.Linq;

namespace Pixie.Unity
{
    public class PXUnityClient : MonoBehaviour
    {
        private const int RECONNECTION_DELAY = 5000;

        private ConcurrentQueue<Action> sendingQueue = new ConcurrentQueue<Action>();

        [SerializeField]
        private string serverHost;

        [SerializeField]
        private int serverPort;

        [SerializeField]
        private bool autoSearchEventHandlers = true;

        [SerializeField]
        private GameObject[] eventKeepingGameObjects;

        private TcpClient socketConnection = null;
        private NetworkStream networkStream = null;
        private PXUnityMessageReader reader = null;
        private PXUnityMessageWriter writer = null;

        private PXUnityMessageHandlerRawBase[] events;

        private void Awake() {
            if (autoSearchEventHandlers) {
                events = UnityEngine.Object.FindObjectsOfType<PXUnityMessageHandlerRawBase>();
            } else {
                if (eventKeepingGameObjects.Length == 0) {
                    eventKeepingGameObjects = new GameObject[] { this.gameObject };
                }

                events = eventKeepingGameObjects
                    .Select(ek => ek.GetComponents<PXUnityMessageHandlerRawBase>())
                    .SelectMany(mh => mh)
                    .ToArray();
            }

            StartCoroutine(StartDataStreamPreparing());
        }

        private void FixedUpdate() {
            ProcessDataStream();
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
                networkStream = socketConnection.GetStream();
                reader = new PXUnityMessageReader(networkStream, events.Select(x => x.DataType).ToArray());
                writer = new PXUnityMessageWriter(networkStream);
                return true;
            } catch (Exception e) {
                FinalizeDataStream();
                Debug.LogError(e);
            }

            return false;
        }

        private void FinalizeDataStream() {
            reader = null;
            writer = null;
            networkStream?.Dispose();
            networkStream = null;
            socketConnection?.Close();
            socketConnection = null;
        }

        private void ProcessDataStream() {
            if (reader == null) {
                return;
            }

            try {
                reader.Update();

                if (reader.HasMessage) {
                    ProcessCommand(reader.DequeueMessage());
                }

                Action sendingAction;

                while (sendingQueue.TryDequeue(out sendingAction)) {
                    sendingAction();
                }
            } catch (SocketException se) {
                Debug.LogError(se);
                FinalizeDataStream();
                StartCoroutine(StartDataStreamPreparing());
            }
        }

        public void SendMessage(object message) {
            sendingQueue.Enqueue(delegate {
                Debug.Log("Sending message: " + message.GetType().Name);

                writer.Send(message);
            });
        }

        private void ProcessCommand(object message) {
            var messageType = message.GetType();

            Debug.Log("Message received: " + messageType.ToString());

            foreach (var e in events) {
                if (e.DataType == messageType) {
                    e.SetupData(message).Execute();
                }
            }
        }

    }
}