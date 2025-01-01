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
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace JpegXLFileTypePlugin.ICCProfile.Numeric
{
    /// <summary>
    /// Represents a signed fixed-point value with 15 integer bits and 16 fractional bits.
    /// </summary>
    /// <seealso cref="IEquatable{S15Fixed16}" />
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct S15Fixed16 : IEquatable<S15Fixed16>, IFormattable
    {
        private readonly int fixedValue;

        public S15Fixed16(ReadOnlySpan<byte> bytes)
            => fixedValue = BinaryPrimitives.ReadInt32BigEndian(bytes);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => ToString(NumberFormatInfo.InvariantInfo);

        public override bool Equals(object? obj) => obj is S15Fixed16 other && Equals(other);

        public bool Equals(S15Fixed16 other) => fixedValue == other.fixedValue;

        public override int GetHashCode() => unchecked(-970009898 + fixedValue.GetHashCode());

        public float ToFloat() => fixedValue / 65536.0f;

        public override string? ToString() => ToString(NumberFormatInfo.CurrentInfo);

        public string ToString(IFormatProvider? formatProvider) => ToString("G", formatProvider);

        public string ToString(string? format, IFormatProvider? formatProvider)
            => ToFloat().ToString(format, formatProvider);

        public static bool operator ==(S15Fixed16 left, S15Fixed16 right) => left.Equals(right);

        public static bool operator !=(S15Fixed16 left, S15Fixed16 right) => !(left == right);

        private sealed class DebugView
        {
            private readonly S15Fixed16 value;

            public DebugView(S15Fixed16 value) => this.value = value;

            public int FixedValue => value.fixedValue;

            public float FloatValue => value.ToFloat();
        }
    }
}
