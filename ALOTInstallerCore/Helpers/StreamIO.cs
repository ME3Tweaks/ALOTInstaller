///*
// * C# Stream Helpers
// *
// * Copyright (C) 2015-2018 Pawel Kolodziejski
// * Copyright (C) 2019 ME3Explorer
// * Copyright (C) 2019 ME3Tweaks
// *
// * This program is free software; you can redistribute it and/or
// * modify it under the terms of the GNU General Public License
// * as published by the Free Software Foundation; either version 2
// * of the License, or (at your option) any later version.
// *
// * This program is distributed in the hope that it will be useful,
// * but WITHOUT ANY WARRANTY; without even the implied warranty of
// * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// * GNU General Public License for more details.
// *
// * You should have received a copy of the GNU General Public License
// * along with this program; if not, write to the Free Software
// * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
// *
// */

//using System;
//using System.ComponentModel;
//using System.IO;
//using System.Text;
//using ALOTInstallerCore.Objects;
//using LegendaryExplorerCore.Packages;

//namespace ALOTInstallerCore.Helpers
//{
//    [Localizable(false)]
//    public static class StreamHelpers
//    {
//        public static byte[] ReadToBuffer(this Stream stream, int count)
//        {
//            var buffer = new byte[count];
//            if (stream.Read(buffer, 0, count) != count)
//                throw new Exception(@"Stream read error!");
//            return buffer;
//        }

//        public static byte[] ReadToBuffer(this Stream stream, uint count)
//        {
//            return stream.ReadToBuffer((int)count);
//        }

//        public static byte[] ReadToBuffer(this Stream stream, long count)
//        {
//            return stream.ReadToBuffer((int)count);
//        }

//        public static void WriteFromBuffer(this Stream stream, byte[] buffer)
//        {
//            stream.Write(buffer, 0, buffer.Length);
//        }

//        public static void WriteFromStream(this Stream stream, Stream inputStream, int count)
//        {
//            var buffer = new byte[0x10000];
//            do
//            {
//                int readed = inputStream.Read(buffer, 0, Math.Min(buffer.Length, count));
//                if (readed > 0)
//                    stream.Write(buffer, 0, readed);
//                else
//                    break;
//                count -= readed;
//            } while (count != 0);
//        }

//        public static void WriteFromStream(this Stream stream, Stream inputStream, uint count)
//        {
//            WriteFromStream(stream, inputStream, (int)count);
//        }

//        public static void WriteFromStream(this Stream stream, Stream inputStream, long count)
//        {
//            WriteFromStream(stream, inputStream, (int)count);
//        }

//        public static string ReadStringASCII(this Stream stream, int count)
//        {
//            byte[] buffer = stream.ReadToBuffer(count);
//            return Encoding.ASCII.GetString(buffer);
//        }

//        public static string ReadStringASCIINull(this Stream stream)
//        {
//            string str = "";
//            for (; ; )
//            {
//                char c = (char)stream.ReadByte();
//                if (c == 0)
//                    break;
//                str += c;
//            }
//            return str;
//        }

//        public static string ReadStringASCIINull(this Stream stream, int count)
//        {
//            return stream.ReadStringASCII(count).Trim('\0');
//        }

//        public static string ReadStringUnicode(this Stream stream, int count)
//        {
//            var buffer = stream.ReadToBuffer(count);
//            return Encoding.Unicode.GetString(buffer);
//        }

//        public static string ReadStringUTF8(this Stream stream, int count)
//        {
//            var buffer = stream.ReadToBuffer(count);
//            return Encoding.UTF8.GetString(buffer);
//        }

//        public static string ReadStringUnicodeNull(this Stream stream, int count)
//        {
//            return stream.ReadStringUnicode(count).Trim('\0');
//        }

//        public static void WriteStringASCII(this Stream stream, string str)
//        {
//            stream.Write(Encoding.ASCII.GetBytes(str), 0, Encoding.ASCII.GetByteCount(str));
//        }

//        public static void WriteStringASCIINull(this Stream stream, string str)
//        {
//            stream.WriteStringASCII(str + '\0');
//        }

//        public static void WriteStringUnicode(this Stream stream, string str)
//        {
//            stream.Write(Encoding.Unicode.GetBytes(str), 0, Encoding.Unicode.GetByteCount(str));
//        }

//        public static void WriteStringUTF8(this Stream stream, string str)
//        {
//            stream.Write(Encoding.UTF8.GetBytes(str), 0, Encoding.UTF8.GetByteCount(str));
//        }

//        public static void WriteStringUnicodeNull(this Stream stream, string str)
//        {
//            stream.WriteStringUnicode(str + '\0');
//        }
        
//    }
//}
