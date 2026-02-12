////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022, 2023, 2024, 2025, 2026 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.IO;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class StreamIOCallbacks
    {
        private readonly Stream stream;
        private readonly WriteDelegate write;
        private readonly SeekDelegate seek;

        public unsafe StreamIOCallbacks(Stream stream)
        {
            this.stream = stream;
            write = Write;
            seek = Seek;
            ExceptionInfo = null;
        }

        public ExceptionDispatchInfo? ExceptionInfo
        {
            get;
            private set;
        }

        public IOCallbacks GetIOCallbacks()
        {
            return new IOCallbacks()
            {
                Write = Marshal.GetFunctionPointerForDelegate(write),
                Seek = Marshal.GetFunctionPointerForDelegate(seek),
            };
        }

        private unsafe int Write(byte* buffer, nuint count)
        {
            int hr = HResult.S_OK;

            try
            {
                if (buffer != null)
                {
                    if (count > 0)
                    {
                        if (count < int.MaxValue)
                        {
                            stream.Write(new ReadOnlySpan<byte>(buffer, (int)count));
                        }
                        else
                        {
                            stream.Write(new ExtentPtr<byte>(buffer, checked((nint)count)));
                        }
                    }
                }
                else
                {
                    hr = HResult.E_POINTER;
                }
            }
            catch (OperationCanceledException)
            {
                hr = HResult.E_ABORT;
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                hr = ex.HResult;
            }

            return hr;
        }

        private int Seek(ulong offset)
        {
            int hr = HResult.S_OK;

            try
            {
                long newPosition = stream.Seek(checked((long)offset), SeekOrigin.Begin);

                if (newPosition != (long)offset)
                {
                    hr = HResult.SeekError;
                }
            }
            catch (OperationCanceledException)
            {
                hr = HResult.E_ABORT;
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                hr = ex.HResult;
            }

            return hr;
        }
    }
}
