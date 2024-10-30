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
using System.Runtime.ExceptionServices;
using System.Text;

namespace JpegXLFileTypePlugin.Interop
{
    internal sealed class DecoderLayerData : Disposable
    {
        public DecoderLayerData()
        {
            Layer = null;
            OwnsLayer = true;
        }

        public ExceptionDispatchInfo? ExceptionInfo { get; private set; }

        public BitmapLayer? Layer { get; private set; }

        public bool OwnsLayer { get; set; }

        public unsafe bool CreateLayer(int width,
                                       int height,
                                       sbyte* name,
                                       uint nameLengthInBytes,
                                       BitmapData* outLayerData)
        {
            try
            {
                string layerName = string.Empty;

                if (name != null && nameLengthInBytes > 0 && nameLengthInBytes <= int.MaxValue)
                {
                    layerName = Encoding.UTF8.GetString((byte*)name, (int)nameLengthInBytes);
                }

                if (string.IsNullOrWhiteSpace(layerName))
                {
                    Layer = PaintDotNet.Layer.CreateBackgroundLayer(width, height);
                }
                else
                {
                    Layer = new BitmapLayer(width, height)
                    {
                        Name = layerName
                    };
                }

                outLayerData->scan0 = (byte*)Layer.Surface.Scan0.VoidStar;
                outLayerData->width = (uint)width;
                outLayerData->height = (uint)height;
                outLayerData->stride = (uint)Layer.Surface.Stride;
            }
            catch (Exception ex)
            {
                ExceptionInfo = ExceptionDispatchInfo.Capture(ex);
                return false;
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Layer != null)
                {
                    if (OwnsLayer)
                    {
                        Layer.Dispose();
                    }
                    Layer = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
