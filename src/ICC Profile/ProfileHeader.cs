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

using JpegXLFileTypePlugin.ICCProfile.Numeric;
using System;
using System.Buffers.Binary;

namespace JpegXLFileTypePlugin.ICCProfile
{
    internal sealed class ProfileHeader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileHeader"/> structure.
        /// </summary>
        /// <param name="profileBytes">The profile bytes.</param>
        public ProfileHeader(ReadOnlySpan<byte> profileBytes)
        {
            Size = BinaryPrimitives.ReadUInt32BigEndian(profileBytes);
            CmmType = new ProfileSignature(profileBytes[4..]);
            Version = new ProfileVersion(profileBytes[8..]);
            DeviceClass = (ProfileClass)BinaryPrimitives.ReadUInt32BigEndian(profileBytes[12..]);
            ColorSpace = (ProfileColorSpace)BinaryPrimitives.ReadUInt32BigEndian(profileBytes[16..]);
            ConnectionSpace = (ProfileColorSpace)BinaryPrimitives.ReadUInt32BigEndian(profileBytes[20..]);
            DateTime = new ProfileDateTime(profileBytes[24..]);
            Signature = new ProfileSignature(profileBytes[36..]);
            Platform = (ProfilePlatform)BinaryPrimitives.ReadUInt32BigEndian(profileBytes[40..]);
            ProfileFlags = BinaryPrimitives.ReadUInt32BigEndian(profileBytes[44..]);
            Manufacturer = new ProfileSignature(profileBytes[48..]);
            Model = new ProfileSignature(profileBytes[52..]);
            Attributes = BinaryPrimitives.ReadUInt64BigEndian(profileBytes[56..]);
            RenderingIntent = (RenderingIntent)BinaryPrimitives.ReadUInt32BigEndian(profileBytes[64..]);
            Illuminant = new XYZNumber(profileBytes[68..]);
            Creator = new ProfileSignature(profileBytes[80..]);
            ID = new ProfileID(profileBytes.Slice(84, 16));
        }

        public uint Size { get; }

        public ProfileSignature CmmType { get; }

        public ProfileVersion Version { get; }

        public ProfileClass DeviceClass { get; }

        public ProfileColorSpace ColorSpace { get; }

        public ProfileColorSpace ConnectionSpace { get; }

        public ProfileDateTime DateTime { get; }

        public ProfileSignature Signature { get; }

        public ProfilePlatform Platform { get; }

        public uint ProfileFlags { get; }

        public ProfileSignature Manufacturer { get; }

        public ProfileSignature Model { get; }

        public ulong Attributes { get; }

        public RenderingIntent RenderingIntent { get; }

        public XYZNumber Illuminant { get; }

        public ProfileSignature Creator { get; }

        public ProfileID ID { get; }
    }
}
