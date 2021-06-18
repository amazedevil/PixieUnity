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
using Pixie.Core.Exceptions;

namespace Pixie.Unity
{
    public class PXUnityClient : MonoBehaviour, IPXProtocolContact
    {
        public class ConnectionException : Exception
        {
            public ConnectionException(Exception e) : base("Initial connection exception", e) { }
        }

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

        [SerializeField]
        private UnityEvent OnConnectionFailedFatal = null;

        private TcpClient connection = null;
        private PXExceptionsFilterStream exceptionsFilter = null;

        private PXUnityMessageEncoder encoder = null;
        private PXUnityMessageHandlerRawBase[] handlers;
        private ConcurrentQueue<Action> mainThreadActionQueue = new ConcurrentQueue<Action>();
        private PXLLProtocol pllpProtocol = new PXLLProtocol();
        private CancellationTokenSource destroyCancelationTokenSource = new CancellationTokenSource();
        private Type protocolType = null;

        public string ClientId { get; private set; } = null;

        protected virtual string ServerHost => serverHost;

        protected virtual int ServerPort => serverPort;

        private void Awake() {
            if (protocol == null) {
                protocolType = typeof(PXReliableDeliveryProtocol);
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

            if (protocol != null) {
                ProtocolInitialize();
            }

            if (autoConnectOnAwake) {
                _ = Connect();
            }
        }

        public async Task Connect() {
            try {
                while (true) {
                    try {
                        if (connection != null) {
                            //we suppose that if we change connection to new,
                            //something went wrong with previous one
                            exceptionsFilter?.SwitchToErrorState();
                            exceptionsFilter = null;
                            connection.Close();
                        }

                        connection = new TcpClient();

                        IPAddress address = new Func<IPAddress>(() => {
                            try {
                                return IPAddress.Parse(ServerHost);
                            } catch (FormatException) {
                                return Dns.GetHostEntry(ServerHost)
                                    .AddressList
                                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                            }
                        })();

                        Task ConnectAsyncFixed(IPAddress aAddress, int aPort, TcpClient aClient) {
                            void EndConnectFixed(IAsyncResult asyncResult) {
                                if (aClient.Client != null) {
                                    aClient.EndConnect(asyncResult); //TODO: process socket exception
                                } else {
                                    throw new ObjectDisposedException(aClient.GetType().FullName);
                                }
                            }

                            return Task.Factory.FromAsync(aClient.BeginConnect, EndConnectFixed, aAddress, aPort, null);
                        }

                        try {
                            await ConnectAsyncFixed(address, ServerPort, connection);
                        } catch (SocketException e) {
                            throw new ConnectionException(e);
                        }

                        //TODO: make ssl support
                        var stream = new PXExceptionsFilterStream(connection.GetStream());

                        var isNewConnection = ClientId != null;

                        ClientId = await pllpProtocol.WelcomeFromSender(stream, ClientId);

                        //initializing new protocol
                        if (this == null) { //check if has been destroyed meanwhile
                            return;
                        }

                        if (protocol == null) {
                            protocol = this.gameObject.AddComponent(protocolType) as PXProtocolBase;
                            ProtocolInitialize();
                        }

                        protocol.SetupStream(exceptionsFilter = stream);

                        OnConnected.Invoke(isNewConnection);

                        return;
                    } catch (Exception e) {
                        if (repeatConnectionAttempts) {
                            Debug.LogException(e);

                            await Task.Delay(TimeSpan.FromSeconds(RECONNECTION_DELAY_SECONDS), destroyCancelationTokenSource.Token);
                        } else {
                            throw;
                        }
                    }
                }
            } catch (TaskCanceledException) {
                //object destroyed
            }
        }

        public void Stop() {
            if (connection == null) {
                return;
            }

            Debug.Log("Stopping Pixie client");

            connection.Close();
            connection = null;

            ResetProtocol();

            OnDispose();
            OnDisposed.Invoke();
        }

        private void ProtocolInitialize() {
            protocol.Initialize(this);

            async void StartReading() {
                await WrapProtocolExceptions(async delegate {
                    await this.protocol.StartReading();
                });
            }

            StartReading();
        }

        private void ResetProtocol() {            
            if (this.protocol != null) {
                this.protocol.Dispose();
                GameObject.Destroy(this.protocol);
                this.protocol = null;
                this.ClientId = null;
            }
        }

        protected virtual void OnDispose() {
        }

        private void FixedUpdate() {
            while (mainThreadActionQueue.TryDequeue(out Action action)) {
                action();
            }
        }

        private void OnDestroy() {
            this.connection?.Close();
            ResetProtocol();

            destroyCancelationTokenSource.Cancel();
        }

        public async void SendMessage(object message) {
            await WrapProtocolExceptions(async delegate {
                await (this.protocol ?? throw new PXConnectionClosedLocalException()).SendMessage(encoder.EncodeMessage(message));
            });
        }

        private async Task WrapProtocolExceptions(Func<Task> action, bool rethrow = false) {
            try {
                await action();
            } catch (PXConnectionClosedLocalException) {
                if (rethrow) {
                    throw;
                }
            } catch (PXConnectionClosedRemoteException) {
                Stop();

                if (rethrow) {
                    throw;
                }
            } catch (Exception e) {
                Debug.LogException(e);

                if (rethrow) {
                    throw new PXConnectionUnknownErrorException(e);
                }
            }
        }

        private void ProcessMessage(object message) {
            var messageType = message.GetType();

            Debug.Log("Message received: " + messageType.ToString());

            foreach (var eh in handlers) {
                if (eh.DataType == messageType) {
                    eh.SetupData(message).Execute();
                }
            }
        }

        private object ProcessRequest(object message) {
            //TODO: make processing of requests from server to client
            throw new NotImplementedException();
        }

        public void ReceivedMessage(byte[] message) {
            ProcessMessage(this.encoder.DecodeMessage(message));
        }

        public void ReceivedRequestMessage(ushort id, byte[] message) {
            _ = WrapProtocolExceptions(async delegate {
                await this.protocol?.SendResponse(id, this.encoder.EncodeMessage(
                    ProcessRequest(this.encoder.DecodeMessage(message))
                ));
            });
        }

        public async Task<R> SendRequestMessage<A, R>(A message) where A : struct where R : struct {
            this.encoder.RegisterMessageTypeIfNotRegistered(typeof(R)); //response type should be registered to be decoded

            R result = default;

            await WrapProtocolExceptions(async delegate {
                result = (R)this.encoder.DecodeMessage(
                    await (this.protocol ?? throw new PXConnectionClosedLocalException())
                        .SendRequestMessage(this.encoder.EncodeMessage(message))
                );
            }, true);

            return result;
        }

        public void OnProtocolStateChanged() {
            if (this.protocol.GetState() == PXProtocolState.WaitingForConnection) {
                this.exceptionsFilter.SwitchToErrorState();

                if (reconnect) {
                    OnDisconnected.Invoke();

                    async void Reconnect() {
                        try {
                            await Connect();
                        } catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }

                    Reconnect();
                } else {
                    OnConnectionFailedFatal.Invoke();

                    Stop();
                }
            }
        }
    }
}