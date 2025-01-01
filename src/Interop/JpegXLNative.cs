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

using PaintDotNet;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.Interop
{
    internal static class JpegXLNative
    {
        internal static Version GetLibJxlVersion()
        {
            uint packedVersionNumber;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                packedVersionNumber = JpegXL_X64.GetLibJxlVersion();
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                packedVersionNumber = JpegXL_Arm64.GetLibJxlVersion();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            int major = (int)((packedVersionNumber >> 24) & 0xff);
            int minor = (int)((packedVersionNumber >> 16) & 0xff);
            int patch = (int)((packedVersionNumber >> 8) & 0xff);

            return new Version(major, minor, patch);
        }

        internal static unsafe void LoadImage(byte[] imageData,
                                              DecoderImage decoderImage)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            ArgumentNullException.ThrowIfNull(decoderImage);

            DecoderStatus status;
            ErrorInfo errorInfo = new();

            DecoderCallbacks callbacks = decoderImage.GetDecoderCallbacks();

            fixed (byte* data = imageData)
            {
                nuint dataSize = (nuint)imageData.Length;

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = JpegXL_X64.LoadImage(callbacks, data, dataSize, ref errorInfo);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = JpegXL_Arm64.LoadImage(callbacks, data, dataSize, ref errorInfo);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }

            GC.KeepAlive(callbacks);

            if (status != DecoderStatus.Ok)
            {
                HandleDecoderError(status, decoderImage, errorInfo);
            }
        }

        internal static unsafe void SaveImage(Surface surface,
                                              EncoderOptions options,
                                              EncoderImageMetadata metadata,
                                              ProgressCallback? progressCallback,
                                              Stream output)
        {
            StreamIOCallbacks streamIO = new(output);

            IOCallbacks callbacks = streamIO.GetIOCallbacks();

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
                status = JpegXL_X64.SaveImage(bitmapData, options, metadata, callbacks, ref errorInfo, progressCallback);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                status = JpegXL_Arm64.SaveImage(bitmapData, options, metadata, callbacks, ref errorInfo, progressCallback);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            GC.KeepAlive(progressCallback);
            GC.KeepAlive(streamIO);

            if (status != EncoderStatus.Ok)
            {
                HandleEncoderError(status, errorInfo, streamIO);
            }
        }

        private static unsafe void HandleDecoderError(DecoderStatus status,
                                                      DecoderImage decoderImageInterop,
                                                      ErrorInfo errorInfo)
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
            else if (status == DecoderStatus.CreateLayerError || status == DecoderStatus.CreateMetadataError)
            {
                ExceptionDispatchInfo? info = decoderImageInterop.ExceptionInfo;

                if (info != null)
                {
                    info.Throw();
                }
                else
                {
                    if (status == DecoderStatus.CreateLayerError)
                    {
                        throw new FormatException("An unspecified error occurred when creating the image layer.");
                    }
                    else
                    {
                        throw new FormatException("An unspecified error occurred when creating the image metadata.");
                    }
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

        private static unsafe void HandleEncoderError(EncoderStatus status, ErrorInfo errorInfo, StreamIOCallbacks streamIO)
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
                ExceptionDispatchInfo? exceptionDispatchInfo = streamIO.ExceptionInfo;

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
