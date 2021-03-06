//Based on SpanHelpers.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace jemalloc
{
    internal static partial class BufferHelpers
    {
        public static int IndexOf(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
        {
            Debug.Assert(searchSpaceLength >= 0);
            Debug.Assert(valueLength >= 0);

            if (valueLength == 0)
                return 0;  // A zero-length sequence is always treated as "found" at the start of the search space.

            byte valueHead = value;
            ref byte valueTail = ref Unsafe.Add(ref value, 1);
            int valueTailLength = valueLength - 1;

            int index = 0;
            for (; ; )
            {
                Debug.Assert(0 <= index && index <= searchSpaceLength); // Ensures no deceptive underflows in the computation of "remainingSearchSpaceLength".
                int remainingSearchSpaceLength = searchSpaceLength - index - valueTailLength;
                if (remainingSearchSpaceLength <= 0)
                    break;  // The unsearched portion is now shorter than the sequence we're looking for. So it can't be there.

                // Do a quick search for the first element of "value".
                int relativeIndex = IndexOf(ref Unsafe.Add(ref searchSpace, index), valueHead, remainingSearchSpaceLength);
                if (relativeIndex == -1)
                    break;
                index += relativeIndex;

                // Found the first element of "value". See if the tail matches.
                if (SequenceEqual(ref Unsafe.Add(ref searchSpace, index + 1), ref valueTail, valueTailLength))
                    return index;  // The tail matched. Return a successful find.

                index++;
            }
            return -1;
        }

        public static int IndexOfAny(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
        {
            Debug.Assert(searchSpaceLength >= 0);
            Debug.Assert(valueLength >= 0);

            if (valueLength == 0)
                return 0;  // A zero-length sequence is always treated as "found" at the start of the search space.

            int index = -1;
            for (int i = 0; i < valueLength; i++)
            {
                var tempIndex = IndexOf(ref searchSpace, Unsafe.Add(ref value, i), searchSpaceLength);
                if (tempIndex != -1)
                {
                    index = (index == -1 || index > tempIndex) ? tempIndex : index;
                }
            }
            return index;
        }



        private const ulong XorPowerOfTwoToHighByte = (0x07ul |
                                                       0x06ul << 8 |
                                                       0x05ul << 16 |
                                                       0x04ul << 24 |
                                                       0x03ul << 32 |
                                                       0x02ul << 40 |
                                                       0x01ul << 48) + 1;

        public static int IndexOf<T>(ref T searchSpace, int searchSpaceLength, ref T value, int valueLength)
            where T : IEquatable<T>
        {
            Debug.Assert(searchSpaceLength >= 0);
            Debug.Assert(valueLength >= 0);

            if (valueLength == 0)
                return 0;  // A zero-length sequence is always treated as "found" at the start of the search space.

            T valueHead = value;
            ref T valueTail = ref Unsafe.Add(ref value, 1);
            int valueTailLength = valueLength - 1;

            int index = 0;
            for (;;)
            {
                Debug.Assert(0 <= index && index <= searchSpaceLength); // Ensures no deceptive underflows in the computation of "remainingSearchSpaceLength".
                int remainingSearchSpaceLength = searchSpaceLength - index - valueTailLength;
                if (remainingSearchSpaceLength <= 0)
                    break;  // The unsearched portion is now shorter than the sequence we're looking for. So it can't be there.

                // Do a quick search for the first element of "value".
                int relativeIndex = IndexOf(ref Unsafe.Add(ref searchSpace, index), valueHead, remainingSearchSpaceLength);
                if (relativeIndex == -1)
                    break;
                index += relativeIndex;

                // Found the first element of "value". See if the tail matches.
                if (SequenceEqual(ref Unsafe.Add(ref searchSpace, index + 1), ref valueTail, valueTailLength))
                    return index;  // The tail matched. Return a successful find.

                index++;
            }
            return -1;
        }

        public static unsafe int IndexOf<T>(ref T searchSpace, T value, int length)
            where T : IEquatable<T>
        {
            Debug.Assert(length >= 0);

            IntPtr index = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            while (length >= 8)
            {
                length -= 8;

                if (value.Equals(Unsafe.Add(ref searchSpace, index)))
                    goto Found;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 1)))
                    goto Found1;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 2)))
                    goto Found2;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 3)))
                    goto Found3;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 4)))
                    goto Found4;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 5)))
                    goto Found5;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 6)))
                    goto Found6;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 7)))
                    goto Found7;

                index += 8;
            }

            if (length >= 4)
            {
                length -= 4;

                if (value.Equals(Unsafe.Add(ref searchSpace, index)))
                    goto Found;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 1)))
                    goto Found1;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 2)))
                    goto Found2;
                if (value.Equals(Unsafe.Add(ref searchSpace, index + 3)))
                    goto Found3;

                index += 4;
            }

            while (length > 0)
            {
                if (value.Equals(Unsafe.Add(ref searchSpace, index)))
                    goto Found;

                index += 1;
                length--;
            }
            return -1;

        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)index;
        Found1:
            return (int)(byte*)(index + 1);
        Found2:
            return (int)(byte*)(index + 2);
        Found3:
            return (int)(byte*)(index + 3);
        Found4:
            return (int)(byte*)(index + 4);
        Found5:
            return (int)(byte*)(index + 5);
        Found6:
            return (int)(byte*)(index + 6);
        Found7:
            return (int)(byte*)(index + 7);
        }

        public static bool SequenceEqual<T>(ref T first, ref T second, int length)
            where T : IEquatable<T>
        {
            Debug.Assert(length >= 0);

            if (Unsafe.AreSame(ref first, ref second))
                goto Equal;

            IntPtr index = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            while (length >= 8)
            {
                length -= 8;

                if (!Unsafe.Add(ref first, index).Equals(Unsafe.Add(ref second, index)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 1).Equals(Unsafe.Add(ref second, index + 1)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 2).Equals(Unsafe.Add(ref second, index + 2)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 3).Equals(Unsafe.Add(ref second, index + 3)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 4).Equals(Unsafe.Add(ref second, index + 4)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 5).Equals(Unsafe.Add(ref second, index + 5)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 6).Equals(Unsafe.Add(ref second, index + 6)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 7).Equals(Unsafe.Add(ref second, index + 7)))
                    goto NotEqual;

                index += 8;
            }

            if (length >= 4)
            {
                length -= 4;

                if (!Unsafe.Add(ref first, index).Equals(Unsafe.Add(ref second, index)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 1).Equals(Unsafe.Add(ref second, index + 1)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 2).Equals(Unsafe.Add(ref second, index + 2)))
                    goto NotEqual;
                if (!Unsafe.Add(ref first, index + 3).Equals(Unsafe.Add(ref second, index + 3)))
                    goto NotEqual;

                index += 4;
            }

            while (length > 0)
            {
                if (!Unsafe.Add(ref first, index).Equals(Unsafe.Add(ref second, index)))
                    goto NotEqual;
                index += 1;
                length--;
            }

        Equal:
            return true;

        NotEqual: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return false;
        }

        /// <summary>
        /// Implements the copy functionality used by Span and ReadOnlySpan.
        ///
        /// NOTE: Fast span implements TryCopyTo in corelib and therefore this implementation
        ///       is only used by portable span. The code must live in code that only compiles
        ///       for portable span which means either each individual span implementation
        ///       of this shared code file. Other shared SpanHelper.X.cs files are compiled
        ///       for both portable and fast span implementations.
        /// </summary>
        public static unsafe void CopyTo<T>(ref T dst, int dstLength, ref T src, int srcLength)
        {
            Debug.Assert(dstLength != 0);

            IntPtr srcByteCount = Unsafe.ByteOffset(ref src, ref Unsafe.Add(ref src, srcLength));
            IntPtr dstByteCount = Unsafe.ByteOffset(ref dst, ref Unsafe.Add(ref dst, dstLength));

            IntPtr diff = Unsafe.ByteOffset(ref src, ref dst);

            bool isOverlapped = (sizeof(IntPtr) == sizeof(int))
                ? (uint)diff < (uint)srcByteCount || (uint)diff > (uint)-(int)dstByteCount
                : (ulong)diff < (ulong)srcByteCount || (ulong)diff > (ulong)-(long)dstByteCount;

            if (!isOverlapped && !BufferHelpers.IsReferenceOrContainsReferences<T>())
            {
                ref byte dstBytes = ref Unsafe.As<T, byte>(ref dst);
                ref byte srcBytes = ref Unsafe.As<T, byte>(ref src);
                ulong byteCount = (ulong)srcByteCount;
                ulong index = 0;

                while (index < byteCount)
                {
                    uint blockSize = (byteCount - index) > uint.MaxValue ? uint.MaxValue : (uint)(byteCount - index);
                    Unsafe.CopyBlock(
                        ref Unsafe.Add(ref dstBytes, (IntPtr)index),
                        ref Unsafe.Add(ref srcBytes, (IntPtr)index),
                        blockSize);
                    index += blockSize;
                }
            }
            else
            {
                bool srcGreaterThanDst = (sizeof(IntPtr) == sizeof(int))
                    ? (uint)diff > (uint)-(int)dstByteCount
                    : (ulong)diff > (ulong)-(long)dstByteCount;

                int direction = srcGreaterThanDst ? 1 : -1;
                int runCount = srcGreaterThanDst ? 0 : srcLength - 1;

                int loopCount = 0;
                for (; loopCount < (srcLength & ~7); loopCount += 8)
                {
                    Unsafe.Add<T>(ref dst, runCount + direction * 0) = Unsafe.Add<T>(ref src, runCount + direction * 0);
                    Unsafe.Add<T>(ref dst, runCount + direction * 1) = Unsafe.Add<T>(ref src, runCount + direction * 1);
                    Unsafe.Add<T>(ref dst, runCount + direction * 2) = Unsafe.Add<T>(ref src, runCount + direction * 2);
                    Unsafe.Add<T>(ref dst, runCount + direction * 3) = Unsafe.Add<T>(ref src, runCount + direction * 3);
                    Unsafe.Add<T>(ref dst, runCount + direction * 4) = Unsafe.Add<T>(ref src, runCount + direction * 4);
                    Unsafe.Add<T>(ref dst, runCount + direction * 5) = Unsafe.Add<T>(ref src, runCount + direction * 5);
                    Unsafe.Add<T>(ref dst, runCount + direction * 6) = Unsafe.Add<T>(ref src, runCount + direction * 6);
                    Unsafe.Add<T>(ref dst, runCount + direction * 7) = Unsafe.Add<T>(ref src, runCount + direction * 7);
                    runCount += direction * 8;
                }
                if (loopCount < (srcLength & ~3))
                {
                    Unsafe.Add<T>(ref dst, runCount + direction * 0) = Unsafe.Add<T>(ref src, runCount + direction * 0);
                    Unsafe.Add<T>(ref dst, runCount + direction * 1) = Unsafe.Add<T>(ref src, runCount + direction * 1);
                    Unsafe.Add<T>(ref dst, runCount + direction * 2) = Unsafe.Add<T>(ref src, runCount + direction * 2);
                    Unsafe.Add<T>(ref dst, runCount + direction * 3) = Unsafe.Add<T>(ref src, runCount + direction * 3);
                    runCount += direction * 4;
                    loopCount += 4;
                }
                for (; loopCount < srcLength; ++loopCount)
                {
                    Unsafe.Add<T>(ref dst, runCount) = Unsafe.Add<T>(ref src, runCount);
                    runCount += direction;
                }
            }
        }

        /// <summary>
        /// Computes "start + index * sizeof(T)", using the unsigned IntPtr-sized multiplication for 32 and 64 bits.
        ///
        /// Assumptions:
        ///     Start and index are non-negative, and already pre-validated to be within the valid range of their containing Span.
        ///
        ///     If the byte length (Span.Length * sizeof(T)) does an unsigned overflow (i.e. the buffer wraps or is too big to fit within the address space),
        ///     the behavior is undefined.
        ///
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr Add<T>(this IntPtr start, int index)
        {
            Debug.Assert(start.ToInt64() >= 0);
            Debug.Assert(index >= 0);

            unsafe
            {
                if (sizeof(IntPtr) == sizeof(int))
                {
                    // 32-bit path.
                    uint byteLength = (uint)index * (uint)Unsafe.SizeOf<T>();
                    return (IntPtr)(((byte*)start) + byteLength);
                }
                else
                {
                    // 64-bit path.
                    ulong byteLength = (ulong)index * (ulong)Unsafe.SizeOf<T>();
                    return (IntPtr)(((byte*)start) + byteLength);
                }
            }
        }

        /// <summary>
        /// Determine if a type is eligible for storage in unmanaged memory.
        /// Portable equivalent of RuntimeHelpers.IsReferenceOrContainsReferences{T}()
        /// </summary>
        public static bool IsReferenceOrContainsReferences<T>() => PerTypeValues<T>.IsReferenceOrContainsReferences;

        private static bool IsReferenceOrContainsReferencesCore(Type type)
        {
            if (type.GetTypeInfo().IsPrimitive) // This is hopefully the common case. All types that return true for this are value types w/out embedded references.
                return false;

            if (!type.GetTypeInfo().IsValueType)
                return true;

            // If type is a Nullable<> of something, unwrap it first.
            Type underlyingNullable = Nullable.GetUnderlyingType(type);
            if (underlyingNullable != null)
                type = underlyingNullable;

            if (type.GetTypeInfo().IsEnum)
                return false;

            foreach (FieldInfo field in type.GetTypeInfo().DeclaredFields)
            {
                if (field.IsStatic)
                    continue;
                if (IsReferenceOrContainsReferencesCore(field.FieldType))
                    return true;
            }
            return false;
        }

        public static class PerTypeValues<T>
        {
            //
            // Latch to ensure that excruciatingly expensive validation check for constructing a Span around a raw pointer is done
            // only once per type.
            //
            public static readonly bool IsReferenceOrContainsReferences = IsReferenceOrContainsReferencesCore(typeof(T));

            public static readonly T[] EmptyArray = new T[0];

            public static readonly IntPtr ArrayAdjustment = MeasureArrayAdjustment();

            // Array header sizes are a runtime implementation detail and aren't the same across all runtimes. (The CLR made a tweak after 4.5, and Mono has an extra Bounds pointer.)
            private static IntPtr MeasureArrayAdjustment()
            {
                T[] sampleArray = new T[1];
                return Unsafe.ByteOffset<T>(ref Unsafe.As<JemPinnable<T>>(sampleArray).Data, ref sampleArray[0]);
            }
        }

        public unsafe static void ClearLessThanPointerSized(byte* ptr, UIntPtr byteLength)
        {
            if (sizeof(UIntPtr) == sizeof(uint))
            {
                Unsafe.InitBlockUnaligned(ptr, 0, (uint)byteLength);
            }
            else
            {
                // PERF: Optimize for common case of length <= uint.MaxValue
                ulong bytesRemaining = (ulong)byteLength;
                uint bytesToClear = (uint)(bytesRemaining & uint.MaxValue);
                Unsafe.InitBlockUnaligned(ptr, 0, bytesToClear);
                bytesRemaining -= bytesToClear;
                ptr += bytesToClear;
                // Clear any bytes > uint.MaxValue
                while (bytesRemaining > 0)
                {
                    bytesToClear = (bytesRemaining >= uint.MaxValue) ? uint.MaxValue : (uint)bytesRemaining;
                    Unsafe.InitBlockUnaligned(ptr, 0, bytesToClear);
                    ptr += bytesToClear;
                    bytesRemaining -= bytesToClear;
                }
            }
        }

        public static unsafe void ClearLessThanPointerSized(ref byte b, UIntPtr byteLength)
        {
            if (sizeof(UIntPtr) == sizeof(uint))
            {
                Unsafe.InitBlockUnaligned(ref b, 0, (uint)byteLength);
            }
            else
            {
                // PERF: Optimize for common case of length <= uint.MaxValue
                ulong bytesRemaining = (ulong)byteLength;
                uint bytesToClear = (uint)(bytesRemaining & uint.MaxValue);
                Unsafe.InitBlockUnaligned(ref b, 0, bytesToClear);
                bytesRemaining -= bytesToClear;
                long byteOffset = bytesToClear;
                // Clear any bytes > uint.MaxValue
                while (bytesRemaining > 0)
                {
                    bytesToClear = (bytesRemaining >= uint.MaxValue) ? uint.MaxValue : (uint)bytesRemaining;
                    ref byte bOffset = ref Unsafe.Add(ref b, (IntPtr)byteOffset);
                    Unsafe.InitBlockUnaligned(ref bOffset, 0, bytesToClear);
                    byteOffset += bytesToClear;
                    bytesRemaining -= bytesToClear;
                }
            }
        }

        public unsafe static void ClearPointerSizedWithoutReferences(ref byte b, UIntPtr byteLength)
        {
            // TODO: Perhaps do switch casing to improve small size perf

            var i = IntPtr.Zero;
            while (i.LessThanEqual(byteLength - sizeof(Reg64)))
            {
                Unsafe.As<byte, Reg64>(ref Unsafe.Add<byte>(ref b, i)) = default(Reg64);
                i += sizeof(Reg64);
            }
            if (i.LessThanEqual(byteLength - sizeof(Reg32)))
            {
                Unsafe.As<byte, Reg32>(ref Unsafe.Add<byte>(ref b, i)) = default(Reg32);
                i += sizeof(Reg32);
            }
            if (i.LessThanEqual(byteLength - sizeof(Reg16)))
            {
                Unsafe.As<byte, Reg16>(ref Unsafe.Add<byte>(ref b, i)) = default(Reg16);
                i += sizeof(Reg16);
            }
            if (i.LessThanEqual(byteLength - sizeof(long)))
            {
                Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, i)) = 0;
                i += sizeof(long);
            }
            // JIT: Should elide this if 64-bit
            if (sizeof(IntPtr) == sizeof(int))
            {
                if (i.LessThanEqual(byteLength - sizeof(int)))
                {
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, i)) = 0;
                    i += sizeof(int);
                }
            }
        }

        public unsafe static void ClearPointerSizedWithReferences(ref IntPtr ip, UIntPtr pointerSizeLength)
        {
            // TODO: Perhaps do switch casing to improve small size perf

            var i = IntPtr.Zero;
            var n = IntPtr.Zero;
            while ((n = i + 8).LessThanEqual(pointerSizeLength))
            {
                Unsafe.Add<IntPtr>(ref ip, i + 0) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 1) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 2) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 3) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 4) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 5) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 6) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 7) = default(IntPtr);
                i = n;
            }
            if ((n = i + 4).LessThanEqual(pointerSizeLength))
            {
                Unsafe.Add<IntPtr>(ref ip, i + 0) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 1) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 2) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 3) = default(IntPtr);
                i = n;
            }
            if ((n = i + 2).LessThanEqual(pointerSizeLength))
            {
                Unsafe.Add<IntPtr>(ref ip, i + 0) = default(IntPtr);
                Unsafe.Add<IntPtr>(ref ip, i + 1) = default(IntPtr);
                i = n;
            }
            if ((i + 1).LessThanEqual(pointerSizeLength))
            {
                Unsafe.Add<IntPtr>(ref ip, i) = default(IntPtr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static bool LessThanEqual(this IntPtr index, UIntPtr length)
        {
            return (sizeof(UIntPtr) == sizeof(uint))
                ? (int)index <= (int)length
                : (long)index <= (long)length;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct Reg64 { }
        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct Reg32 { }
        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct Reg16 { }
    }
}
