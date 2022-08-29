////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-jpegxl, a FileType plugin for Paint.NET
// that loads and saves JPEG XL images.
//
// Copyright (c) 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal static class JpegXLNative
    {
        internal static SafeDecoderContext CreateDecoder()
        {
            SafeDecoderContext context;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                context = JpegXL_X64.CreateDecoder();
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                context = JpegXL_Arm64.CreateDecoder();
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                context = JpegXL_X86.CreateDecoder();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (context is null || context.IsInvalid)
            {
                ExceptionUtil.ThrowInvalidOperationException("Unable to create the HEIC file context.");
            }

            return context;
        }

        internal static unsafe void DecodeFile(SafeDecoderContext context, byte[] imageData, out DecoderImageInfo imageInfo)
        {
            ArgumentNullException.ThrowIfNull(imageData);

            imageInfo = new DecoderImageInfo();

            DecoderStatus status;
            ErrorInfo errorInfo = new();

            fixed (byte* data = imageData)
            {
                nuint dataSize = (nuint)imageData.Length;

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = JpegXL_X64.DecodeFile(context, data, dataSize, imageInfo, ref errorInfo);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = JpegXL_Arm64.DecodeFile(context, data, dataSize, imageInfo, ref errorInfo);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                {
                    status = JpegXL_X86.DecodeFile(context, data, dataSize, imageInfo, ref errorInfo);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }

            if (status != DecoderStatus.Ok)
            {
                HandleDecoderError(status, errorInfo);
            }
        }

        internal static unsafe void GetIccProfileData(SafeDecoderContext context, byte* data, nuint dataSize)
        {
            DecoderStatus status;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                status = JpegXL_X64.GetIccProfileData(context, data, dataSize);
            }
            else if(RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                status = JpegXL_Arm64.GetIccProfileData(context, data, dataSize);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                status = JpegXL_X86.GetIccProfileData(context, data, dataSize);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (status != DecoderStatus.Ok)
            {
                HandleDecoderError(status);
            }
        }

        internal static unsafe void CopyDecodedPixelsToSurface(SafeDecoderContext context, Surface surface)
        {
            ArgumentNullException.ThrowIfNull(surface);

            BitmapData bitmapData = new()
            {
                scan0 = (byte*)surface.Scan0.VoidStar,
                width = (uint)surface.Width,
                height = (uint)surface.Height,
                stride = (uint)surface.Stride
            };

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                JpegXL_X64.CopyDecodedPixelsToSurface(context, ref bitmapData);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                JpegXL_Arm64.CopyDecodedPixelsToSurface(context, ref bitmapData);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                JpegXL_X86.CopyDecodedPixelsToSurface(context, ref bitmapData);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        internal static unsafe void SaveImage(Surface surface,
                                              EncoderOptions options,
                                              EncoderImageMetadata? metadata,
                                              ProgressCallback? progressCallback,
                                              Stream output)
        {
            using (JpegXLStreamWriter streamWriter = new(output, ownsStream: false))
            {
                WriteDataCallback writeDataCallback = streamWriter.WriteDataCallback;

                BitmapData bitmapData = new()
                {
                    scan0 = (byte*)surface.Scan0.VoidStar,
                    width = (uint)surface.Width,
                    height = (uint)surface.Height,
                    stride = (uint)surface.Stride
                };

                ErrorInfo errorInfo;

                EncoderStatus status;

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = JpegXL_X64.SaveImage(ref bitmapData, options, metadata, ref errorInfo, progressCallback, writeDataCallback);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = JpegXL_Arm64.SaveImage(ref bitmapData, options, metadata, ref errorInfo, progressCallback, writeDataCallback);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                {
                    status = JpegXL_X86.SaveImage(ref bitmapData, options, metadata, ref errorInfo, progressCallback, writeDataCallback);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                GC.KeepAlive(progressCallback);
                GC.KeepAlive(writeDataCallback);

                if (status != EncoderStatus.Ok)
                {
                    HandleEncoderError(status, errorInfo, streamWriter);
                }
            }
        }

        private static unsafe void HandleDecoderError(DecoderStatus status, ErrorInfo errorInfo = default)
        {
            if (status == DecoderStatus.DecodeError)
            {
                string message = new(errorInfo.errorMessage);

                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new FormatException("An unspecified error occurred when decoding the image.");
                }
                else
                {
                    throw new FormatException(message);
                }
            }
            else
            {
                switch (status)
                {
                    case DecoderStatus.Ok:
                        break;
                    case DecoderStatus.NullParameter:
                        throw new InvalidOperationException("A required native API parameter was null.");
                    case DecoderStatus.InvalidParameter:
                        throw new InvalidOperationException("A native API parameter was invalid.");
                    case DecoderStatus.BufferTooSmall:
                        throw new InvalidOperationException("A native API buffer parameter was too small.");
                    case DecoderStatus.OutOfMemory:
                        throw new OutOfMemoryException();
                    case DecoderStatus.HasAnimation:
                        throw new FormatException("The image is an animation.");
                    case DecoderStatus.HasMultipleFrames:
                        throw new FormatException("The image has multiple frames.");
                    case DecoderStatus.ImageDimensionExceedsInt32:
                        throw new FormatException("The image dimensions are too large.");
                    case DecoderStatus.UnsupportedChannelFormat:
                        throw new FormatException("The image uses an unsupported color channel format.");
                    case DecoderStatus.MetadataError:
                        throw new FormatException("An error occurred when decoding the image meta data.");
                    default:
                        throw new FormatException("An unspecified error occurred when decoding the image.");
                }
            }
        }

        private static unsafe void HandleEncoderError(EncoderStatus status, ErrorInfo errorInfo, JpegXLStreamWriter streamWriter)
        {
            if (status == EncoderStatus.EncodeError)
            {
                string message = new(errorInfo.errorMessage);

                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new FormatException("An unspecified error occurred when encoding the image.");
                }
                else
                {
                    throw new FormatException(message);
                }
            }
            else if (status == EncoderStatus.WriteError)
            {
                ExceptionDispatchInfo? exceptionDispatchInfo = streamWriter.ExceptionInfo;

                if (exceptionDispatchInfo != null)
                {
                    exceptionDispatchInfo.Throw();
                }
                else
                {
                    throw new FormatException("An unspecified error occurred when writing the image data.");
                }
            }
            else
            {
                switch (status)
                {
                    case EncoderStatus.Ok:
                        break;
                    case EncoderStatus.NullParameter:
                        throw new InvalidOperationException("A required native API parameter was null.");
                    case EncoderStatus.OutOfMemory:
                        throw new OutOfMemoryException();
                    case EncoderStatus.UserCancelled:
                        throw new OperationCanceledException();
                    default:
                        throw new FormatException("An unspecified error occurred when encoding the image.");
                }
            }
        }
    }
}
