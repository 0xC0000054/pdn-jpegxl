////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JpegXLFileTypePlugin.Exif
{
    internal sealed class StreamSegment : Stream
    {
        private readonly Stream stream;
        private bool isOpen;
        private readonly long origin;
        private readonly long length;
        private readonly bool leaveOpen;

        public StreamSegment(Stream stream, long origin, long length, bool leaveOpen = false)
        {
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));
            ArgumentOutOfRangeException.ThrowIfNegative(origin, nameof(origin));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0, nameof(length));

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("The stream must support reading and seeking.");
            }

            if (checked(origin + length) > stream.Length)
            {
                throw new ArgumentException("Invalid stream origin and length.");
            }

            this.stream = stream;
            this.origin = this.stream.Position = origin;
            this.length = origin + length;
            this.leaveOpen = leaveOpen;
            isOpen = true;
        }

        public override bool CanRead => isOpen;

        public override bool CanSeek => isOpen;

        public override bool CanTimeout => isOpen && stream.CanTimeout;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                VerifyNotDisposed();

                return length - origin;
            }
        }

        public override long Position
        {
            get
            {
                VerifyNotDisposed();

                return stream.Position - origin;
            }
            set
            {
                VerifyNotDisposed();

                long current = Position;

                if (value != current)
                {
                    long newPosition = unchecked(origin + value);

                    if (newPosition < origin)
                    {
                        throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                            "The value is less than the segment origin, value: 0x{0:X} origin: 0x{1:X}",
                                                                            newPosition,
                                                                            origin));
                    }

                    if (newPosition > length)
                    {
                        throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                            "The value is greater than the segment length, value: 0x{0:X} length: 0x{1:X}",
                                                                            newPosition,
                                                                            length));
                    }

                    stream.Position = newPosition;
                }
            }
        }

        public override int ReadTimeout
        {
            get
            {
                VerifyNotDisposed();

                return stream.ReadTimeout;
            }
            set
            {
                VerifyNotDisposed();

                stream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                VerifyNotDisposed();

                return stream.WriteTimeout;
            }
            set
            {
                VerifyNotDisposed();

                stream.WriteTimeout = value;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            VerifyNotDisposed();

            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if ((Position + count) > Length)
            {
                count = (int)(Length - Position);
            }

            return stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override void Close() => stream?.Close();

        public override void CopyTo(Stream destination, int bufferSize)
        {
            VerifyNotDisposed();

            stream.CopyTo(destination, bufferSize);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();

            return stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override ValueTask DisposeAsync() => stream?.DisposeAsync() ?? ValueTask.CompletedTask;

        public override int EndRead(IAsyncResult asyncResult)
        {
            VerifyNotDisposed();

            return stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            VerifyNotDisposed();

            stream.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            // No-op because the stream is read-only.
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            VerifyNotDisposed();

            // No-op because the stream is read-only.
            return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            VerifyNotDisposed();

            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));

            if ((Position + count) > Length)
            {
                count = (int)(Length - Position);
            }

            return stream.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            VerifyNotDisposed();

            int count = buffer.Length;

            if ((Position + count) > Length)
            {
                count = (int)(Length - Position);
            }

            return stream.Read(buffer.Slice(0, count));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));

            if ((Position + count) > Length)
            {
                count = (int)(Length - Position);
            }

            return stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            VerifyNotDisposed();

            int count = buffer.Length;

            if ((Position + count) > Length)
            {
                count = (int)(Length - Position);
            }

            return stream.ReadAsync(buffer.Slice(0, count), cancellationToken);
        }

        public override int ReadByte()
        {
            VerifyNotDisposed();

            if ((Position + sizeof(byte)) > Length)
            {
                return -1;
            }

            return stream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            VerifyNotDisposed();

            long tempPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    tempPosition = unchecked(this.origin + offset);
                    break;
                case SeekOrigin.Current:
                    tempPosition = unchecked(Position + offset);
                    break;
                case SeekOrigin.End:
                    tempPosition = unchecked(length + offset);
                    break;
                default:
                    throw new ArgumentException("Unknown SeekOrigin value.");
            }

            if (tempPosition < this.origin || tempPosition > length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "The offset is not within the stream segment.");
            }

            return stream.Seek(tempPosition, origin);
        }

        public override void SetLength(long value)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override void WriteByte(byte value)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isOpen = false;

                if (!leaveOpen)
                {
                    stream.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private static void StreamSegmentReadOnly()
        {
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        private void VerifyNotDisposed()
        {
            ObjectDisposedException.ThrowIf(!isOpen, this);
        }
    }
}
