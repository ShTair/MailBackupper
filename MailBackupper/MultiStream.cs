using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailBackupper
{
    class MultiStream : Stream
    {
        private Stream[] _streams;

        public MultiStream(params Stream[] streams)
        {
            _streams = streams;
        }

        public override bool CanRead { get { return _streams.All(t => t.CanRead); } }

        public override bool CanSeek { get { return _streams.All(t => t.CanSeek); } }

        public override bool CanWrite { get { return _streams.All(t => t.CanWrite); } }

        public override void Flush() { AllDo(t => t.Flush()); }

        public override long Length { get { throw new NotImplementedException(); } }

        public override long Position
        {
            get { return Min(AllGet(t => t.Position)); }
            set { AllDo(t => t.Position = value); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            AllDo(t => t.Write(buffer, offset, count));
        }

        private void AllDo(Action<Stream> action)
        {
            foreach (var stream in _streams)
            {
                action(stream);
            }
        }

        private IEnumerable<T> AllGet<T>(Func<Stream, T> func)
        {
            return _streams.Select(func);
        }

        private long Min(IEnumerable<long> src)
        {
            var m = long.MaxValue;
            foreach (var v in src)
            {
                if (m > v) m = v;
            }

            if (m == long.MaxValue) m = 0;

            return m;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AllDo(t => t.Dispose());
            }

            base.Dispose(disposing);
        }
    }
}
