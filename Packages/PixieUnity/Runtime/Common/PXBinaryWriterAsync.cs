using System;
using System.IO;
using System.Threading.Tasks;

namespace Pixie.Unity
{
    internal class PXBinaryWriterAsync
    {
        private Stream innerStream;
        private MemoryStream buffer = new MemoryStream();

        public PXBinaryWriterAsync(Stream innerStream) {
            this.innerStream = innerStream;
        }

        public void Write(short value) {
            this.Write(BitConverter.GetBytes(value));
        }

        public void Write(byte value) {
            this.Write(new byte[] { value });
        }

        public void Write(bool value) {
            this.Write(value ? (byte)1 : (byte)0);
        }

        public void Write(ushort value) {
            this.Write(BitConverter.GetBytes(value));
        }

        public void Write(int value) {
            this.Write(BitConverter.GetBytes(value));
        }

        public void Write(Guid value) {
            this.Write(value.ToByteArray());
        }

        public void Write(byte[] value) {
            this.buffer.Write(value, 0, value.Length);
        }

        public Task FlushAsync() {
            //Unity editor sometimes hangs when writing async, so we made it sync
            var bytes = this.buffer.ToArray();
            this.buffer = new MemoryStream();
            this.innerStream.Write(bytes, 0, bytes.Length);

            return Task.CompletedTask;
        }
    }
}
