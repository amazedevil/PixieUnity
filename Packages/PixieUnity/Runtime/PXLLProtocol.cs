using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Pixie.Unity
{
    public class PXLLProtocol
    {
        public class PLLPVersionIncorrectException : Exception { }
        public class PLLPUknownException : Exception 
        {
            public PLLPUknownException(Exception e) : base("Unknown PLLP Exception", e) { }
        }
        public class PLLPCanceledException : Exception
        {
            public PLLPCanceledException(Exception e) : base("PLLP Canceled Exception", e) { }
        }

        private const short PLLP_VERSION = 1;

        private const short PLLP_SIGNAL_VERSION_OK = 1;
        private const short PLLP_SIGNAL_INCORRECT_VERSION = 2;

        public async Task<string> WelcomeFromReceiver(Stream stream) {
            try {
                var reader = new PXBinaryReaderAsync(stream);
                var writer = new PXBinaryWriterAsync(stream);

                if (await reader.ReadInt16() == PLLP_VERSION) {
                    writer.Write(PLLP_SIGNAL_VERSION_OK);
                    await writer.FlushAsync();
                } else {
                    writer.Write(PLLP_SIGNAL_INCORRECT_VERSION);
                    await writer.FlushAsync();

                    throw new PLLPVersionIncorrectException();
                }

                Guid id;

                if (await reader.ReadBool()) { //true if client has reconnection id, false if connection is new
                    id = (await reader.ReadGuid());
                } else {
                    id = Guid.NewGuid();
                    writer.Write(id);
                    await writer.FlushAsync();
                }

                return id.ToString();
            } catch (Exception e) {
                throw new PLLPUknownException(e);
            }
        }

        public async Task<string> WelcomeFromSender(Stream stream, string reconnectingClientId) {
            try {
                var reader = new PXBinaryReaderAsync(stream);
                var writer = new PXBinaryWriterAsync(stream);

                writer.Write(PLLP_VERSION);
                await writer.FlushAsync();

                if (await reader.ReadInt16() != PLLP_SIGNAL_VERSION_OK) {
                    throw new PLLPVersionIncorrectException();
                }

                if (reconnectingClientId != null) {
                    writer.Write(true);
                    writer.Write(Guid.Parse(reconnectingClientId));
                    await writer.FlushAsync();
                    return reconnectingClientId;
                } else {
                    writer.Write(false);
                    await writer.FlushAsync();
                    return (await reader.ReadGuid()).ToString();
                }
            } catch (ObjectDisposedException e) {
                //That's client code only case, cause
                //for client we suppose it can interrupt
                //connection while handshake, for server
                //it's always treated as connection problem

                throw new PLLPCanceledException(e);
            } catch (Exception e) {
                throw new PLLPUknownException(e);
            }
        }
    }
}
