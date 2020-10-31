using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

namespace Pixie.Unity
{
    public class PXUnityClient : MonoBehaviour, IPXProtocolContact
    {
        [Serializable]
        public class ConnectedEvent : UnityEvent<bool> { }

        private const float RECONNECTION_DELAY_SECONDS = 5f;

        [SerializeField]
        protected string serverHost = "localhost";

        [SerializeField]
        protected int serverPort = 7777;

        [SerializeField]
        private bool autoSearchEventHandlers = true;

        [SerializeField]
        private GameObject[] eventKeepingGameObjects;

        [SerializeField]
        private PXProtocolBase protocol = null;

        [SerializeField]
        private bool autoConnectOnAwake = true;

        [SerializeField]
        private bool reconnect = false;

        [SerializeField]
        private bool repeatConnectionAttempts = false;

        [SerializeField]
        private UnityEvent OnDisconnected = null;

        [SerializeField]
        private UnityEvent OnDisposed = null;

        [SerializeField]
        private ConnectedEvent OnConnected = null;

        private TcpClient socketConnection = null;
        private PXUnityMessageEncoder encoder = null;
        private PXUnityMessageHandlerRawBase[] handlers;
        private ConcurrentQueue<Action> mainThreadActionQueue = new ConcurrentQueue<Action>();
        private PXLLProtocol pllpProtocol = new PXLLProtocol();
        private CancellationTokenSource destroyCancelationTokenSource = new CancellationTokenSource();
        private Type protocolType;

        public string ClientId { get; private set; } = null;

        private void Awake() {
            if (protocol == null) {
                protocolType = typeof(PXReliableDeliveryProtocol);
                ResetProtocol();
            } else {
                protocolType = protocol.GetType();
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

            if (autoConnectOnAwake) {
                _ = Connect();
            }
        }

        public async Task Connect() {
            await StartDataStreamPreparing();
        }

        public void Disconnect() {
            ResetProtocol();

            socketConnection?.Close();
            socketConnection = null;

            OnDispose();

            OnDisposed.Invoke();
        }

        private void ResetProtocol() {
            if (this.protocol != null) {
                this.protocol.Dispose();
                GameObject.Destroy(this.protocol);
            }

            protocol = this.gameObject.AddComponent(protocolType) as PXProtocolBase;
            protocol.Initialize(this);
            StartReading();
        }

        protected virtual void OnDispose() {
        }

        protected virtual void OnConnectionFail() {
        }

        private void FixedUpdate() {
            while (mainThreadActionQueue.TryDequeue(out Action action)) {
                action();
            }
        }

        private void OnDestroy() {
            this.protocol.Dispose();
            this.socketConnection?.Close();

            destroyCancelationTokenSource.Cancel();
        }

        private async Task StartDataStreamPreparing() {
            try {
                while (!await PrepareDataStream()) {
                    OnConnectionFail();

                    if (!repeatConnectionAttempts) {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(RECONNECTION_DELAY_SECONDS), destroyCancelationTokenSource.Token);
                }
            } catch (TaskCanceledException) {
                //object destroyed
            }
        }

        private async Task<bool> PrepareDataStream() {
            try {
                socketConnection = new TcpClient();

                IPAddress address = null;

                try {
                    address = IPAddress.Parse(serverHost);
                } catch (FormatException) {
                    address = Dns.GetHostEntry(serverHost)
                        .AddressList
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                }

                await socketConnection.ConnectAsync(address, serverPort);

                var stream = socketConnection.GetStream();

                var isNewConnection = ClientId != null;

                ClientId = await pllpProtocol.WelcomeFromSender(stream, ClientId);

                protocol.SetupStream(new PXExceptionsFilterStream(stream));

                OnConnected.Invoke(isNewConnection);

                return true;
            } catch (Exception e) {
                socketConnection?.Close();
                socketConnection = null;

                Debug.LogError(e);
            }

            return false;
        }

        private async void StartReading() {
            await WrapProtocolExceptions(async delegate {
                await this.protocol.StartReading();
            });
        }

        public async void SendMessage(object message) {
            await WrapProtocolExceptions(async delegate {
                await this.protocol.SendMessage(encoder.EncodeMessage(message));
            });
        }

        private async Task WrapProtocolExceptions(Func<Task> action) {
            try {
                await action();
            } catch (PXConnectionFinishedException) {
                //do nothing
            } catch (PXConnectionClosedException) {
                //do nothing
            } catch (Exception e) {
                ClientException(e);
            }
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

        public void ReceivedMessage(byte[] message) {
            var decoded = this.encoder.DecodeMessage(message);
            mainThreadActionQueue.Enqueue(delegate {
                ProcessCommand(decoded);
            });
        }

        public void ReceivedRequestMessage(ushort id, byte[] message) {
            this.protocol.SendResponse(id, message);
        }

        public async Task<R> SendRequestMessage<A, R>(A message) where A : struct where R : struct {
            this.encoder.RegisterMessageTypeIfNotRegistered(typeof(R)); //response type should be registered to be decoded

            return (R)this.encoder.DecodeMessage(await this.protocol.SendRequestMessage(this.encoder.EncodeMessage(message)));
        }

        public void OnProtocolStateChanged() {
            if (this.protocol.GetState() == PXProtocolState.WaitingForConnection) {
                if (reconnect) {
                    OnDisconnected.Invoke();

                    _ = StartDataStreamPreparing();
                } else {
                    Disconnect();
                }
            }
        }

        public void ClientException(Exception e) {
            mainThreadActionQueue.Enqueue(delegate {
                Debug.LogError(e);
            });
        }
    }
}