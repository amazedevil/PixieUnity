using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pixie.Unity
{
    internal class PXExceptionsFilterStream : Stream
    {
        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => innerStream.CanSeek;

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => innerStream.Length;

        public override long Position { get => this.innerStream.Position; set => this.innerStream.Position = value; }

        private Stream innerStream;

        public PXExceptionsFilterStream(Stream innerStream) {
            this.innerStream = innerStream;
        }

        public override void Flush() {
            this.innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return this.innerStream.Read(buffer, offset, count);
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            if (count == 0) {
                return 0;
            }

            return await WrapStreamFuncOperation(async delegate {
                var bytesRead = await this.innerStream.ReadAsync(buffer, offset, count, cancellationToken);

                if (bytesRead == 0) {
                    throw new PXConnectionFinishedException();
                }

                return bytesRead;
            });
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return this.innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            this.innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            WrapStreamSyncActionOperation(delegate {
                this.innerStream.Write(buffer, offset, count);
            });
        }

        public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            await WrapStreamActionOperation(async delegate {
                await this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            });
        }

        private async Task<T> WrapStreamFuncOperation<T>(Func<Task<T>> func) {
            T result = default;         
            
            await WrapStreamActionOperation(async delegate {
                result = await func();
            });

            return result;
        }

        private void WrapStreamSyncActionOperation(Action action) {
            try {
                WrapStreamActionOperation(() => {
                    action();

                    return Task.CompletedTask;
                }).Wait();
            } catch (AggregateException ae) {
                throw ae.InnerException;
            }
        }

        private async Task WrapStreamActionOperation(Func<Task> action) {
            try {
                await action();
            } catch (SocketException) {
                //almost any socket exception means
                //connection loosing for us
                ThrowLostOrClosed();
            } catch (ObjectDisposedException) {
                //network stream seems to be closed, so we get this error,
                //we excpect it, so do nothing
                ThrowLostOrClosed();
            } catch (IOException) {
                //that happens sometimes, if user closes connection
                ThrowLostOrClosed();
            }
        }

        private void ThrowLostOrClosed() {
            throw new PXConnectionLostException();
        }
    }
}
