/*
 * SevenZip Helper
 *
 * Copyright (C) 2015 Pawel Kolodziejski <aquadran at users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ALOTInstallerCore.Helpers;
using ME3ExplorerCore.Packages;

namespace SevenZipHelper
{
    [Localizable(false)]
    public static class LZMA
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipUnpackFile([In] string archive, [In] string outputpath, [In] int keepArchivePaths);

        public static byte[] Decompress(byte[] src, uint dstLen)
        {
            uint len = dstLen;
            byte[] dst = new byte[dstLen];

            int status = SevenZipDecompress(src, (uint)src.Length, dst, ref len);
            if (status != 0)
                return new byte[0];

            return dst;
        }

        public static byte[] Compress(byte[] src, int compressionLevel = 9)
        {
            uint dstLen = (uint)(src.Length * 2 + 8);
            byte[] tmpbuf = new byte[dstLen];

            int status = SevenZipCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
            if (status != 0)
                return new byte[0];

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

            return dst;
        }

        public static bool ExtractSevenZipArchive(string archive, string outputpath, bool keepArchivePath = true)
        {
            Directory.CreateDirectory(outputpath); //must exist
            var result = SevenZipUnpackFile(archive, outputpath, keepArchivePath ? 1 : 0);
            return result == 0;
        }

        /// <summary>
        /// Compresses the input data and returns LZMA compressed data, with the proper header for an LZMA file.
        /// </summary>
        /// <param name="src">Source data</param>
        /// <returns>Byte array of compressed data</returns>

        public static byte[] CompressToLZMAFile(byte[] src)
        {
            var compressedBytes = SevenZipHelper.LZMA.Compress(src);
            byte[] fixedBytes = new byte[compressedBytes.Length + 8]; //needs 8 byte header written into it (only mem version needs this)
            Buffer.BlockCopy(compressedBytes, 0, fixedBytes, 0, 5);
            fixedBytes.OverwriteRange(5, BitConverter.GetBytes(src.Length));
            Buffer.BlockCopy(compressedBytes, 5, fixedBytes, 13, compressedBytes.Length - 5);
            return fixedBytes;
        }

        internal static byte[] DecompressLZMAFile(byte[] lzmaFile)
        {
            int len = (int)BitConverter.ToInt32(lzmaFile, 5); //this is technically a 32-bit but since MEM code can't handle 64 bit sizes we are just going to use 32bit.

            if (len >= 0)
            {
                byte[] strippedData = new byte[lzmaFile.Length - 8];
                //Non-Streamed (made from disk)
                Buffer.BlockCopy(lzmaFile, 0, strippedData, 0, 5);
                Buffer.BlockCopy(lzmaFile, 13, strippedData, 5, lzmaFile.Length - 13);
                return Decompress(strippedData, (uint)len);
            }
            else if (len == -1)
            {
                throw new Exception("Cannot decompress streamed LZMA with this implementation!");
            }
            else
            {
                Debug.WriteLine(@"Cannot decompress LZMA array: Length is not positive or -1 (" + len + @")! This is not an LZMA array");
                return null; //Not LZMA!
            }
        }

        internal static void DecompressLZMAStream(MemoryStream compressedStream, MemoryStream decompressedStream)
        {
            compressedStream.Seek(5, SeekOrigin.Begin);
            int len = compressedStream.ReadInt32();
            compressedStream.Seek(0, SeekOrigin.Begin);

            if (len >= 0)
            {
                byte[] strippedData = new byte[compressedStream.Length - 8];
                compressedStream.Read(strippedData, 0, 5);
                compressedStream.Seek(8, SeekOrigin.Current); //Skip 8 bytes for length.
                compressedStream.Read(strippedData, 5, (int)compressedStream.Length - 13);
                var decompressed = Decompress(strippedData, (uint)len);
                decompressedStream.Write(decompressed);
            }
            else if (len == -1)
            {
                throw new Exception("Cannot decompress streamed LZMA with this implementation!");
            }
            else
            {
                Debug.WriteLine(@"LZMA Stream to decompess has wrong length: " + len);
            }
        }
    }
}
