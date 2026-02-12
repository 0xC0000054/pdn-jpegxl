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

using System;
using System.Buffers.Binary;
using System.Diagnostics;

namespace JpegXLFileTypePlugin.ICCProfile
{
    [DebuggerDisplay("{ToString(), nq}")]
    internal readonly struct ProfileDateTime : IEquatable<ProfileDateTime>
    {
        public ProfileDateTime(ReadOnlySpan<byte> bytes)
        {
            Year = BinaryPrimitives.ReadUInt16BigEndian(bytes);
            Month = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..]);
            Day = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
            Hour = BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]);
            Minute = BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]);
            Second = BinaryPrimitives.ReadUInt16BigEndian(bytes[10..]);
        }

        public ushort Year { get; }

        public ushort Month { get; }

        public ushort Day { get; }

        public ushort Hour { get; }

        public ushort Minute { get; }

        public ushort Second { get; }

        public override bool Equals(object? obj) => obj is ProfileDateTime other && Equals(other);

        public bool Equals(ProfileDateTime other) => Year == other.Year &&
                                                     Month == other.Month &&
                                                     Day == other.Day &&
                                                     Hour == other.Hour &&
                                                     Minute == other.Minute &&
                                                     Second == other.Second;

        public override int GetHashCode() => HashCode.Combine(Year, Month, Day, Hour, Minute, Second);

        public DateTime ToDateTime() => new(Year, Month, Day, Hour, Minute, Second);

        public override string ToString() => ToDateTime().ToString();

        public static bool operator ==(ProfileDateTime left, ProfileDateTime right) => left.Equals(right);

        public static bool operator !=(ProfileDateTime left, ProfileDateTime right) => !(left == right);
    }
}
