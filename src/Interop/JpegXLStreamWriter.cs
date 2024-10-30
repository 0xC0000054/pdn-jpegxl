////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class JpegXLStreamWriter : Disposable
    {
        private readonly Stream stream;
        private readonly bool ownsStream;

        public unsafe JpegXLStreamWriter(Stream stream, bool ownsStream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            this.stream = stream;
            this.ownsStream = ownsStream;
            WriteDataCallback = WriteData;
        }

        public WriteDataCallback WriteDataCallback { get; }

        public ExceptionDispatchInfo? ExceptionInfo { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ownsStream)
                {
                    stream.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private unsafe bool WriteData(byte* buffer, nuint bufferSize)
        {
            try
            {
                if (bufferSize > 0)
                {
                    if (bufferSize > int.MaxValue)
                    {
                        nuint bytesWritten = 0;
                        nuint bytesRemaining = bufferSize;

                        while (bytesRemaining > 0)
                        {
                            nuint bytesToWrite = Math.Min(bytesRemaining, int.MaxValue);

                            stream.Write(new ReadOnlySpan<byte>(buffer + bytesWritten, (int)bytesToWrite));

                            bytesWritten += bytesToWrite;
                            bytesRemaining -= bytesToWrite;
                        }
                    }
                    else
                    {
                        stream.Write(new ReadOnlySpan<byte>(buffer, (int)bufferSize));
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }
    }
}
