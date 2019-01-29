﻿// <copyright file="Base16.cs" company="Sedat Kapanoglu">
// Copyright (c) 2014-2019 Sedat Kapanoglu
// Licensed under Apache-2.0 License (see LICENSE.txt file for details)
// </copyright>

namespace SimpleBase
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Hexadecimal encoding/decoding
    /// </summary>
    public static class Base16
    {
        /// <summary>
        /// Encode to Base16 representation using uppercase lettering
        /// </summary>
        /// <param name="bytes">Bytes to encode</param>
        /// <returns>Base16 string</returns>
        public static unsafe string EncodeUpper(ReadOnlySpan<byte> bytes)
        {
            return internalEncode(bytes, 'A');
        }

        /// <summary>
        /// Encode to Base16 representation using lowercase lettering
        /// </summary>
        /// <param name="bytes">Bytes to encode</param>
        /// <returns>Base16 string</returns>
        public static unsafe string EncodeLower(ReadOnlySpan<byte> bytes)
        {
            return internalEncode(bytes, 'a');
        }

        /// <summary>
        /// Decode an encoded text into bytes
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Result bytes</returns>
        public static Span<byte> Decode(string text)
        {
            Require.NotNull(text, nameof(text));
            return Decode(text.AsSpan());
        }

        /// <summary>
        /// Decode Base16 text into bytes
        /// </summary>
        /// <param name="text">Base16 text</param>
        /// <returns>Decoded bytes</returns>
        public static unsafe Span<byte> Decode(ReadOnlySpan<char> text)
        {
            int textLen = text.Length;
            if (textLen == 0)
            {
                return Array.Empty<byte>();
            }

            // remainder operator ("%") was unexpectedly slow here
            // that's why we're using "&" below
            if ((textLen & 1) != 0)
            {
                throw new ArgumentException("Text cannot be odd length", nameof(text));
            }

            byte[] output = new byte[textLen >> 1];
            fixed (byte* outputPtr = output)
            fixed (char* textPtr = text)
            {
                byte* pOutput = outputPtr;
                char* pInput = textPtr;
                char* pEnd = pInput + textLen;
                while (pInput != pEnd)
                {
                    char c1 = *pInput++;
                    int b1 = getHexByte(c1);
                    char c2 = *pInput++;
                    int b2 = getHexByte(c2);
                    *pOutput = (byte)(b1 << 4 | b2);
                    pOutput++;
                }
            }

            return output;
        }

        /// <summary>
        /// Decode Base16 text through streams for generic use. Stream based variant tries to consume
        /// as little memory as possible, and relies of .NET's own underlying buffering mechanisms,
        /// contrary to their buffer-based versions.
        /// </summary>
        /// <param name="input">Stream that the encoded bytes would be read from</param>
        /// <param name="output">Stream where decoded bytes will be written to</param>
        public static void Decode(TextReader input, Stream output)
        {
            Require.NotNull(input, nameof(input));
            Require.NotNull(output, nameof(output));
            const int bufferSize = 4096;
            var buffer = new char[bufferSize];
            using (var writer = new BinaryWriter(output))
            {
                while (true)
                {
                    int bytesRead = input.Read(buffer, 0, bufferSize);
                    if (bytesRead < 1)
                    {
                        break;
                    }

                    var result = Decode(buffer.AsSpan(0, bytesRead));
                    writer.Write(result.ToArray());
                }
            }
        }

        /// <summary>
        /// Encodes stream of bytes into a Base16 text
        /// </summary>
        /// <param name="input">Stream that provides bytes to be encoded</param>
        /// <param name="output">Stream that the encoded text is written to</param>
        public static void EncodeUpper(Stream input, TextWriter output)
        {
            internalEncode(input, output, 'A');
        }

        /// <summary>
        /// Encodes stream of bytes into a Base16 text
        /// </summary>
        /// <param name="input">Stream that provides bytes to be encoded</param>
        /// <param name="output">Stream that the encoded text is written to</param>
        public static void EncodeLower(Stream input, TextWriter output)
        {
            internalEncode(input, output, 'a');
        }

        private static unsafe string internalEncode(ReadOnlySpan<byte> bytes, char baseChar)
        {
            int bytesLen = bytes.Length;
            if (bytesLen == 0)
            {
                return string.Empty;
            }

            var output = new string('\0', bytesLen << 1);
            fixed (char* outputPtr = output)
            fixed (byte* bytesPtr = bytes)
            {
                char* pOutput = outputPtr;
                byte* pInput = bytesPtr;

                char a = baseChar;

                byte hex(byte b) => (b < 10) ? (byte)('0' + b) : (byte)(a + b - 10);

                int octets = bytesLen / 2;
                for (int i = 0; i < octets; i++, pInput += 2, pOutput += 4)
                {
                    // reduce memory accesses by reading and writing 8 bytes at once
                    ushort pair = *(ushort*)pInput;
                    ulong pad = hex((byte)((pair >> 4) & 0x0F))
                            | ((ulong)hex((byte)(pair & 0x0F)) << 16)
                            | ((ulong)hex((byte)(pair >> 12)) << 32)
                            | ((ulong)hex((byte)((pair >> 8) & 0x0F)) << 48);
                    *((ulong*)pOutput) = pad;
                }

                if (bytesLen % 2 > 0)
                {
                    byte b = *pInput++;
                    *pOutput++ = (char)hex((byte)(b >> 4));
                    *pOutput++ = (char)hex((byte)(b & 0x0F));
                }
            }

            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int getHexByte(int c)
        {
            int n = c - '0';
            if (n < 0)
            {
                goto Error;
            }

            if (n < 10)
            {
                return n;
            }

            n = (c | ' ') - 'a' + 10;
            if (n < 0)
            {
                goto Error;
            }

            if (n <= 'z' - 'a')
            {
                return n;
            }

        Error:
            throw new ArgumentException($"Invalid hex character: {c}");
        }

        private static void internalEncode(Stream input, TextWriter output, char baseChar)
        {
            const int bufferLength = 4096;

            var buffer = new byte[bufferLength];
            while (true)
            {
                int bytesRead = input.Read(buffer, 0, bufferLength);
                if (bytesRead < 1)
                {
                    break;
                }

                var result = internalEncode(buffer.AsSpan(0, bytesRead), baseChar);
                output.Write(result);
            }
        }
    }
}