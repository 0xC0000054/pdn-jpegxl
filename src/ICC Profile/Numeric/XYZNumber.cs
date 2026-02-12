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
using System.Diagnostics;
using System.Globalization;

namespace JpegXLFileTypePlugin.ICCProfile.Numeric
{
    [DebuggerDisplay("{ToString(), nq}")]
    internal readonly struct XYZNumber : IEquatable<XYZNumber>
    {
        public XYZNumber(ReadOnlySpan<byte> bytes)
        {
            X = new S15Fixed16(bytes);
            Y = new S15Fixed16(bytes[4..]);
            Z = new S15Fixed16(bytes[8..]);
        }

        public S15Fixed16 X { get; }

        public S15Fixed16 Y { get; }

        public S15Fixed16 Z { get; }

        public override bool Equals(object? obj) => obj is XYZNumber other && Equals(other);

        public bool Equals(XYZNumber other) => X == other.X && Y == other.Y && Z == other.Z;

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
                                                           "X = {0}, Y = {1}, Z = {2}",
                                                           X.ToString(CultureInfo.InvariantCulture),
                                                           Y.ToString(CultureInfo.InvariantCulture),
                                                           Z.ToString(CultureInfo.InvariantCulture));

        public static bool operator ==(XYZNumber left, XYZNumber right) => left.Equals(right);

        public static bool operator !=(XYZNumber left, XYZNumber right) => !(left == right);
    }
}
