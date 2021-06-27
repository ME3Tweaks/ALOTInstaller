using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using ALOTInstallerCore.Objects;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Flattens an exception and writes it to the log as Error
        /// </summary>
        /// <returns>Printable string</returns>
        public static void WriteToLog(this Exception exception, string prefix = "")
        {
            while (exception != null)
            {
                var line1 = exception.GetType().Name + ": " + exception.Message;
                foreach (var line in line1.Split("\n"))
                {
                    Log.Error(prefix + line);
                }

                if (exception.StackTrace != null)
                {
                    foreach (var line in exception.StackTrace.Split("\n"))
                    {
                        Log.Error(prefix + line);
                    }
                }

                exception = exception.InnerException;
            }
        }

        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <returns>Printable string</returns>
        public static string Flatten(this Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        public static string FlattenWithTrace(this Exception exception)
        {
            //Get a StackTrace object for the exception
            StackTrace st = new StackTrace(exception, true);

            //Get the first stack frame
            StackFrame frame = st.GetFrame(0);

            //Get the file name
            string fileName = frame.GetFileName();

            //Get the method name
            string methodName = frame.GetMethod().Name;

            //Get the line number from the stack frame
            int line = frame.GetFileLineNumber();

            //Get the column number
            int col = frame.GetFileColumnNumber();


            var stringBuilder = new StringBuilder();
            if (fileName != null && methodName != null)
            {
                stringBuilder.AppendLine($"{exception.GetType().Name} occurred in {fileName} {methodName}() on line {line}, column {col}");
            }

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }

        public static Version ToVersion(this FileVersionInfo fvi) => new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);

        private static readonly char[] InvalidPathingChars;

        static Extensions()
        {
            List<char> enumerable = new List<char>();
            enumerable.AddRange((IEnumerable<char>)Path.GetInvalidFileNameChars());
            enumerable.AddRange((IEnumerable<char>)Path.GetInvalidPathChars());
            Extensions.InvalidPathingChars = enumerable.ToArray<char>(enumerable.Count);
        }

        public static string GetGameName(this MEGame game)
        {
            if (game == MEGame.ME1) return "Mass Effect";
            if (game == MEGame.ME2) return "Mass Effect 2";
            if (game == MEGame.ME3) return "Mass Effect 3";
            return "Error: Unknown game";
        }

        public static MEGame ApplicableGameToMEGame(this ApplicableGame game)
        {
            if (game == ApplicableGame.ME1) return MEGame.ME1;
            if (game == ApplicableGame.ME2) return MEGame.ME2;
            if (game == ApplicableGame.ME3) return MEGame.ME3;
            return MEGame.Unknown;
        }

        public static ApplicableGame ToApplicableGame(this MEGame game)
        {
            if (game == MEGame.ME1) return ApplicableGame.ME1;
            if (game == MEGame.ME2) return ApplicableGame.ME2;
            if (game == MEGame.ME3) return ApplicableGame.ME3;
            return ApplicableGame.None;
        }

        public static string ToCommaUIString(this ApplicableGame game)
        {
            string res = "";
            if (game.HasFlag(ApplicableGame.ME1)) res += "ME1";
            if (game.HasFlag(ApplicableGame.ME2))
            {
                if (res.Length > 0) res += ", ";
                res += "ME2";
            }
            if (game.HasFlag(ApplicableGame.ME3))
            {
                if (res.Length > 0) res += ", ";
                res += "ME3";
            }
            return res;
        }

        /// <summary>
        /// Checks if a list is ascending basded on the given comparison function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="compareTo"></param>
        /// <returns></returns>
        public static bool IsAscending<T>(this IEnumerable<T> self, Func<T, T, int> compareTo)
        {
            var list = self as IList<T> ?? self.ToList();
            if (list.Count < 2)
            {
                return true;
            }
            T a = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                T b = list[i];
                if (compareTo(a, b) > 0)
                {
                    return false;
                }
                a = b;
            }
            return true;
        }

        /// <summary>
        /// Extracts a sub array from another array with a specified number of elements.
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="oldArray">Current array.</param>
        /// <param name="offset">Start index in oldArray.</param>
        /// <param name="length">Length to extract.</param>
        /// <returns>New array containing elements within the specified range.</returns>
        public static T[] GetRange<T>(this T[] oldArray, int offset, int length)
        {
            T[] objArray = new T[length];
            Array.Copy((Array)oldArray, offset, (Array)objArray, 0, length);
            return objArray;
        }


        /// <summary>
        /// Extracts a sub array from another array starting at offset and reading to end.
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="oldArray">Current array.</param>
        /// <param name="offset">Start index in oldArray.</param>
        /// <returns>New array containing elements within the specified range.</returns>
        public static T[] GetRange<T>(this T[] oldArray, int offset)
        {
            return oldArray.GetRange<T>(offset, oldArray.Length - offset);
        }
        public static bool IsEmpty<T>(this ICollection<T> list)
        {
            return list.Count == 0;
        }

        /// <summary>
        /// Write data to this stream at the current position from another stream at it's current position.
        /// </summary>
        /// <param name="TargetStream">Stream to copy from.</param>
        /// <param name="SourceStream">Stream to copy to.</param>
        /// <param name="Length">Number of bytes to read.</param>
        /// <param name="bufferSize">Size of buffer to use while copying.</param>
        /// <returns>Number of bytes read.</returns>
        public static int ReadFrom(
          this Stream TargetStream,
          Stream SourceStream,
          int Length,
          int bufferSize = 4096)
        {
            byte[] buffer = new byte[bufferSize];
            int num = 0;
            do
            {
                int count = SourceStream.Read(buffer, 0, Math.Min(bufferSize, Length));
                if (count != 0)
                {
                    Length -= count;
                    TargetStream.Write(buffer, 0, count);
                    num += count;
                }
                else
                    break;
            }
            while (Length > 0);
            return num;
        }

        /// <summary>
        /// Reads an int from stream at the current position and advances 4 bytes.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Integer read from stream.</returns>
        public static int ReadInt32FromStream(this Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.Default, true))
                return binaryReader.ReadInt32();
        }

        /// <summary>
        /// Reads an uint from stream at the current position and advances 4 bytes.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Integer read from stream.</returns>
        public static uint ReadUInt32FromStream(this Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.Default, true))
                return binaryReader.ReadUInt32();
        }

        public static void WriteToFile(this MemoryStream stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (FileStream file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static void WriteInt64ToStream(this Stream stream, long value)
        {
            using (BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.Default, true))
                binaryWriter.Write(value);
        }

        /// <summary>Reads a long from stream at the current position.</summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>Long read from stream.</returns>
        public static long ReadInt64FromStream(this Stream stream)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.Default, true))
                return binaryReader.ReadInt64();
        }

        /// <summary>
        /// Reads a number of bytes from stream at the current position and advances that number of bytes.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="Length">Number of bytes to read.</param>
        /// <returns>Bytes read from stream.</returns>
        public static byte[] ReadBytesFromStream(this Stream stream, int Length)
        {
            using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.Default, true))
                return binaryReader.ReadBytes(Length);
        }

        /// <summary>
        /// Reads a string from a stream. Must be null terminated or have the length written at the start (Pascal strings or something?)
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="HasLengthWritten">True = Attempt to read string length from stream first.</param>
        /// <returns>String read from stream.</returns>
        public static string ReadStringFromStream(this Stream stream, bool HasLengthWritten = false)
        {
            if (stream == null || !stream.CanRead)
                throw new IOException(@"Stream cannot be read.");
            List<char> enumerable = new List<char>();
            if (HasLengthWritten)
            {
                int num = stream.ReadInt32FromStream();
                for (int index = 0; index < num; ++index)
                    enumerable.Add((char)stream.ReadByte());
            }
            else
            {
                char ch;
                while ((ch = (char)stream.ReadByte()) > char.MinValue)
                    enumerable.Add(ch);
            }
            return new string(enumerable.ToArray<char>(enumerable.Count));
        }

        public static byte[] Slice(this byte[] src, int start, int length)
        {
            var slice = new byte[length];
            Buffer.BlockCopy(src, start, slice, 0, length);
            return slice;
        }

        /// <summary>
        /// Overwrites a portion of an array starting at offset with the contents of another array.
        /// Accepts negative indexes
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="dest">Array to write to</param>
        /// <param name="offset">Start index in dest. Can be negative (eg. last element is -1)</param>
        /// <param name="source">data to write to dest</param>
        public static void OverwriteRange<T>(this IList<T> dest, int offset, IList<T> source)
        {
            if (offset < 0)
            {
                offset = dest.Count + offset;
                if (offset < 0)
                {
                    throw new IndexOutOfRangeException(@"Attempt to write before the beginning of the array.");
                }
            }
            if (offset + source.Count > dest.Count)
            {
                throw new IndexOutOfRangeException(@"Attempt to write past the end of the array.");
            }
            for (int i = 0; i < source.Count; i++)
            {
                dest[offset + i] = source[i];
            }
        }

        public static T[] TypedClone<T>(this T[] src)
        {
            return (T[])src.Clone();
        }

        /// <summary>
        /// Creates a shallow copy
        /// </summary>
        public static List<T> Clone<T>(this IEnumerable<T> src)
        {
            return new List<T>(src);
        }

        /// <summary>
        /// KFreon: Borrowed this from the DevIL C# Wrapper found here: https://code.google.com/p/devil-net/
        /// 
        /// Reads a stream until the end is reached into a byte array. Based on
        /// <a href="http://www.yoda.arachsys.com/csharp/readbinary.html">Jon Skeet's implementation</a>.
        /// It is up to the caller to dispose of the stream.
        /// </summary>
        /// <param name="stream">Stream to read all bytes from</param>
        /// <param name="initialLength">Initial buffer length, default is 32K</param>
        /// <returns>The byte array containing all the bytes from the stream</returns>
        public static byte[] ReadStreamFully(this Stream stream, int initialLength = 32768)
        {
            stream.Seek(0L, SeekOrigin.Begin);
            if (initialLength < 1)
                initialLength = 32768;
            byte[] buffer = new byte[initialLength];
            int length = 0;
            int num1;
            while ((num1 = stream.Read(buffer, length, buffer.Length - length)) > 0)
            {
                length += num1;
                if (length == buffer.Length)
                {
                    int num2 = stream.ReadByte();
                    if (num2 == -1)
                        return buffer;
                    byte[] numArray = new byte[buffer.Length * 2];
                    Array.Copy((Array)buffer, (Array)numArray, buffer.Length);
                    numArray[length] = (byte)num2;
                    buffer = numArray;
                    ++length;
                }
            }
            byte[] numArray1 = new byte[length];
            Array.Copy((Array)buffer, (Array)numArray1, length);
            return numArray1;
        }

        /// <summary>
        /// FROM GIBBED.
        /// Writes an int to stream at the current position.
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        /// <param name="value">Integer to write.</param>
        public static void WriteInt32ToStream(this Stream stream, int value)
        {
            using (BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.Default, true))
                binaryWriter.Write(value);
        }

        public static T MaxBy<T, R>(this IEnumerable<T> en, Func<T, R> evaluate) where R : IComparable<R>
        {
            return en.Select(t => (obj: t, key: evaluate(t)))
                .Aggregate((max, next) => next.key.CompareTo(max.key) > 0 ? next : max).obj;
        }

        public static T MinBy<T, R>(this IEnumerable<T> en, Func<T, R> evaluate) where R : IComparable<R>
        {
            return en.Select(t => (obj: t, key: evaluate(t)))
                .Aggregate((max, next) => next.key.CompareTo(max.key) < 0 ? next : max).obj;
        }

        /// <summary>
        /// Returns true if <paramref name="path"/> starts with the path <paramref name="baseDirPath"/>.
        /// The comparison is case-insensitive, handles / and \ slashes as folder separators and
        /// only matches if the base dir folder name is matched exactly (@"c:\foobar\file.txt" is not a sub path of "c:\foo").
        /// </summary>
        public static bool IsSubPathOf(this string path, string baseDirPath)
        {
            string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
                .WithEnding(@"\\"));

            string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
                .WithEnding(@"\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
        /// results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding(@"llo") returns "hello", which is the result of "hel" + "lo".</example>
        public static string WithEnding(this string str, string ending)
        {
            if (str == null)
                return ending;

            string result = str;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (int i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                    return tmp;
            }

            return result;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        public static string Right(this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(@"value");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(@"length", length, @"Length is less than zero");
            }

            return (length < value.Length) ? value.Substring(value.Length - length) : value;
        }

        /// <summary>
        /// Writes string to stream. Terminated by a null char, and optionally writes string length at start of string. (Pascal strings?)
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        /// <param name="str">String to write.</param>
        /// <param name="WriteLength">True = Writes str length before writing string.</param>
        public static void WriteStringToStream(this Stream stream, string str, bool WriteLength = false)
        {
            if (WriteLength)
                stream.WriteInt32ToStream(str.Length);
            foreach (char ch in str)
                stream.WriteByte((byte)ch);
            stream.WriteByte((byte)0);
        }

        /// <summary>Returns index of minimum value based on comparer.</summary>
        /// <param name="enumerable">Collection to search.</param>
        /// <param name="comparer">Comparer to use. e.g. item =&gt; item - x</param>
        /// <returns>Index of minimum value in enumerable based on comparer.</returns>
        public static int IndexOfMin(this IEnumerable<int> enumerable, Func<int, int> comparer)
        {
            int num1 = int.MaxValue;
            int num2 = 0;
            int num3 = 0;
            foreach (int num4 in enumerable)
            {
                int num5 = comparer(num4);
                if (num5 < num1)
                {
                    num1 = num5;
                    num3 = num2;
                }
                ++num2;
            }
            return num3;
        }

        /// <summary>Returns index of minimum value based on comparer.</summary>
        /// <param name="enumerable">Collection to search.</param>
        /// <param name="comparer">Comparer to use. e.g. item =&gt; item - x</param>
        /// <returns>Index of minimum value in enumerable based on comparer.</returns>
        public static int IndexOfMin(this IEnumerable<byte> enumerable, Func<byte, int> comparer)
        {
            int num1 = int.MaxValue;
            int num2 = 0;
            int num3 = 0;
            foreach (byte num4 in enumerable)
            {
                int num5 = comparer(num4);
                if (num5 < num1)
                {
                    num1 = num5;
                    num3 = num2;
                }
                ++num2;
            }
            return num3;
        }

        /// <summary>
        /// Adds elements of a Dictionary to another Dictionary. No checking for duplicates.
        /// </summary>
        /// <typeparam name="T">Key.</typeparam>
        /// <typeparam name="U">Value.</typeparam>
        /// <param name="mainDictionary">Dictionary to add to.</param>
        /// <param name="newAdditions">Dictionary of elements to be added.</param>
        public static void AddRange<T, U>(
          this Dictionary<T, U> mainDictionary,
          Dictionary<T, U> newAdditions)
        {
            if (newAdditions == null)
                throw new ArgumentNullException();
            foreach (KeyValuePair<T, U> newAddition in newAdditions)
                mainDictionary.Add(newAddition.Key, newAddition.Value);
        }

        /// <summary>Add range of elements to given collection.</summary>
        /// <typeparam name="T">Type of items in collection.</typeparam>
        /// <param name="collection">Collection to add to.</param>
        /// <param name="additions">Elements to add.</param>
        public static void AddRangeKinda<T>(this ConcurrentBag<T> collection, IEnumerable<T> additions)
        {
            foreach (T addition in additions)
                collection.Add(addition);
        }

        /// <summary>Add range of elements to given collection.</summary>
        /// <typeparam name="T">Type of items in collection.</typeparam>
        /// <param name="collection">Collection to add to.</param>
        /// <param name="additions">Elements to add.</param>
        public static void AddRangeKinda<T>(this ICollection<T> collection, IEnumerable<T> additions)
        {
            foreach (T addition in additions)
                collection.Add(addition);
        }

        /// <summary>Removes element from collection at index.</summary>
        /// <typeparam name="T">Type of objects in collection.</typeparam>
        /// <param name="collection">Collection to remove from.</param>
        /// <param name="index">Index to remove from.</param>
        /// <returns>Removed element.</returns>
        public static T Pop<T>(this ICollection<T> collection, int index)
        {
            T obj = collection.ElementAt<T>(index);
            collection.Remove(obj);
            return obj;
        }

        /// <summary>
        /// Converts enumerable to List in a more memory efficient way by providing size of list.
        /// </summary>
        /// <typeparam name="T">Type of elements in lists.</typeparam>
        /// <param name="enumerable">Enumerable to convert to list.</param>
        /// <param name="size">Size of list.</param>
        /// <returns>List containing enumerable contents.</returns>
        public static List<T> ToList<T>(this IEnumerable<T> enumerable, int size)
        {
            return new List<T>(enumerable);
        }

        /// <summary>
        /// Converts enumerable to array in a more memory efficient way by providing size of list.
        /// </summary>
        /// <typeparam name="T">Type of elements in list.</typeparam>
        /// <param name="enumerable">Enumerable to convert to array.</param>
        /// <param name="size">Size of lists.</param>
        /// <returns>Array containing enumerable elements.</returns>
        public static T[] ToArray<T>(this IEnumerable<T> enumerable, int size)
        {
            T[] objArray = new T[size];
            int num = 0;
            foreach (T obj in enumerable)
                objArray[num++] = obj;
            return objArray;
        }

        /// <summary>Splits string on (possibly) multiple elements.</summary>
        /// <param name="str">String to split.</param>
        /// <param name="options">Options to use while splitting.</param>
        /// <param name="splitStrings">Elements to split string on. (Delimiters)</param>
        /// <returns></returns>
        public static string[] Split(
          this string str,
          StringSplitOptions options,
          params string[] splitStrings)
        {
            return str.Split(splitStrings, options);
        }

        /// <summary>Compares strings with culture and case sensitivity.</summary>
        /// <param name="str">Main string to check in.</param>
        /// <param name="toCheck">Substring to check for in Main String.</param>
        /// <param name="CompareType">Type of comparison.</param>
        /// <returns>True if toCheck found in str, false otherwise.</returns>
        public static bool Contains(this string str, string toCheck, StringComparison CompareType)
        {
            return str.IndexOf(toCheck, CompareType) >= 0;
        }

        /// <summary>Removes invalid characters from path.</summary>
        /// <param name="str">String to remove chars from.</param>
        /// <returns>New string containing no invalid characters.</returns>
        public static string GetPathWithoutInvalids(this string str)
        {
            StringBuilder stringBuilder = new StringBuilder(str);
            foreach (char invalidPathingChar in Extensions.InvalidPathingChars)
                stringBuilder.Replace(invalidPathingChar.ToString() ?? "", ""); //do not localize
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets parent directory, optionally to a certain depth (or height?)
        /// </summary>
        /// <param name="str">String (hopefully path) to get parent of.</param>
        /// <param name="depth">Depth to get parent of.</param>
        /// <returns>Parent of string.</returns>
        public static string GetDirParent(this string str, int depth = 1)
        {
            string path = (string)null;
            try
            {
                path = Path.GetDirectoryName(str.Trim(Path.DirectorySeparatorChar));
                for (int index = 1; index < depth; ++index)
                    path = Path.GetDirectoryName(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"Failed to get parent directory: " + ex.Message);
            }
            return path;
        }

        public static IEnumerable<string> GetFiles(string path,
            string searchPatternExpression = "",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Regex reSearchPattern = new Regex(searchPatternExpression, RegexOptions.IgnoreCase);
            return Directory.EnumerateFiles(path, "*", searchOption)
                .Where(file =>
                    reSearchPattern.IsMatch(Path.GetExtension(file)));
        }

        /// <summary>
        /// Determines if string is a Directory.
        /// Returns True if directory, false otherwise.
        /// </summary>
        /// <param name="str">String to check.</param>
        /// <returns>True if is a directory, false if not.</returns>
        public static bool isDirectory(this string str)
        {
            return str != null && (File.Exists(str) || Directory.Exists(str)) && File.GetAttributes(str).HasFlag((Enum)FileAttributes.Directory);
        }

        /// <summary>
        /// Determines if string is a file.
        /// Returns True if file, false otherwise.
        /// </summary>
        /// <param name="str">String to check.</param>
        /// <returns>True if a file, false if not</returns>
        public static bool isFile(this string str)
        {
            return !str.isDirectory();
        }

        /// <summary>Determines if string is a number.</summary>
        /// <param name="str">String to check.</param>
        /// <returns>True if string is a number.</returns>
        public static bool isDigit(this string str)
        {
            int result = -1;
            return int.TryParse(str, out result);
        }

        /// <summary>Determines if character is a number.</summary>
        /// <param name="c">Character to check.</param>
        /// <returns>True if c is a number.</returns>
        public static bool isDigit(this char c)
        {
            return (c.ToString() ?? "").isDigit();
        }

        /// <summary>Determines if character is a letter.</summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool isLetter(this char c)
        {
            return !c.isDigit();
        }

        /// <summary>Determines if string is a letter.</summary>
        /// <param name="str">String to check.</param>
        /// <returns>True if str is a letter.</returns>
        public static bool isLetter(this string str)
        {
            if (str.Length == 1)
                return !str.isDigit();
            return false;
        }
    }

    public static class HttpClientExtensions
    {
        public static bool StartsWith(this byte[] thisArray, byte[] otherArray)
        {
            // Handle invalid/unexpected input
            // (nulls, thisArray.Length < otherArray.Length, etc.)

            for (int i = 0; i < otherArray.Length; ++i)
            {
                if (thisArray[i] != otherArray[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class DictionaryExtensions
    {
        /// <summary>
        /// Adds <paramref name="value"/> to List&lt;<typeparamref name="TValue"/>&gt; associated with <paramref name="key"/>. Creates List&lt;<typeparamref name="TValue"/>&gt; if neccesary.
        /// </summary>
        public static void AddToListAt<TKey, TValue>(this Dictionary<TKey, List<TValue>> dict, TKey key, TValue value)
        {
            if (!dict.TryGetValue(key, out List<TValue> list))
            {
                list = new List<TValue>();
                dict[key] = list;
            }
            list.Add(value);
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
    }

    [Localizable(false)]
    public static class StringExtensions
    {
        public static bool isNumericallyEqual(this string first, string second)
        {
            return double.TryParse(first, out double a)
                && double.TryParse(second, out double b)
                && (Math.Abs(a - b) < double.Epsilon);
        }

        public static bool ContainsAny(this string input, IEnumerable<string> containsKeywords, StringComparison comparisonType)
        {
            return containsKeywords.Any(keyword => input.IndexOf(keyword, comparisonType) >= 0);
        }

        // Step 2: https://stackoverflow.com/questions/2435894/net-how-do-i-check-for-illegal-characters-in-a-path
        public static bool ContainsAnyInvalidCharacters(this string path)
        {
            return (!string.IsNullOrEmpty(path) && path.IndexOfAny(Path.GetInvalidPathChars()) >= 0);
        }

        //Step 3: https://stackoverflow.com/questions/2435894/net-how-do-i-check-for-illegal-characters-in-a-path
        public static string RemoveSpecialCharactersUsingFrameworkMethod(this string path)
        {
            return Path.GetInvalidFileNameChars().Aggregate(path, (current, c) => current.Replace(c.ToString(), string.Empty));
        }


        public static Stream ToStream(this string s, bool bom = false)
        {
            return s.ToStream(/*bom ? new UTF8Encoding(false) : */Encoding.UTF8);
        }

        public static Stream ToStream(this string s, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(s ?? ""));
        }

        public static bool RepresentsPackageFilePath(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(@".pcc", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".sfm", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".u", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".upk", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

        public static bool RepresentsFileArchive(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(@".rar", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".7z", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".zip", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

        public static bool RepresentsExtractableItem(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(@".rar", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".7z", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".zip", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".exe", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

        //based on algorithm described here: http://www.codeproject.com/Articles/13525/Fast-memory-efficient-Levenshtein-algorithm
        public static int LevenshteinDistance(this string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            if (n == 0)
            {
                return m;
            }
            if (m == 0)
            {
                return n;
            }

            var v1 = new int[m + 1];
            for (int i = 0; i <= m; i++)
            {
                v1[i] = i;
            }

            for (int i = 1; i <= n; i++)
            {
                int[] v0 = v1;
                v1 = new int[m + 1];
                v1[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int above = v1[j - 1] + 1;
                    int left = v0[j] + 1;
                    int cost;
                    if (j > m || j > n)
                    {
                        cost = 1;
                    }
                    else
                    {
                        cost = a[j - 1] == b[j - 1] ? 0 : 1;
                    }
                    cost += v0[j - 1];
                    v1[j] = Math.Min(above, Math.Min(left, cost));

                }
            }

            return v1[m];
        }

        public static bool FuzzyMatch(this IEnumerable<string> words, string word, double threshold = 0.75)
        {
            foreach (string s in words)
            {
                int dist = s.LevenshteinDistance(word);
                if (1 - (double)dist / Math.Max(s.Length, word.Length) > threshold)
                {
                    return true;
                }
            }
            return false;
        }



        public static Guid ToGuid(this string src) //Do not edit this function!
        {
            byte[] stringbytes = Encoding.UTF8.GetBytes(src);
            byte[] hashedBytes = new System.Security.Cryptography.SHA1CryptoServiceProvider().ComputeHash(stringbytes);
            Array.Resize(ref hashedBytes, 16);
            return new Guid(hashedBytes);
        }
    }

    public static class IOExtensions
    {

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, IEqualityComparer<TSource> comparer = null)
        {
            return IndexOf(source, value, 0, comparer);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, int startIndex, IEqualityComparer<TSource> comparer = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(startIndex));
            }

            var collection = source as ICollection<TSource> ?? source.ToList();

            if ((uint)startIndex >= (uint)collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            comparer = comparer ?? EqualityComparer<TSource>.Default;
            var idx = startIndex;

            foreach (var item in collection.Skip(startIndex))
            {
                if (comparer.Equals(item, value))
                {
                    return idx;
                }

                idx++;
            }

            return -1;
        }

        public static void WriteGuid(this Stream stream, Guid value)
        {
            var data = value.ToByteArray();

            Debug.Assert(data.Length == 16);

            stream.WriteInt32(BitConverter.ToInt32(data, 0));
            stream.WriteInt16(BitConverter.ToInt16(data, 4));
            stream.WriteInt16(BitConverter.ToInt16(data, 6));
            stream.Write(data, 8, 8);
        }

        public static Guid ReadGuid(this Stream stream)
        {
            var a = stream.ReadInt32();
            var b = stream.ReadInt16();
            var c = stream.ReadInt16();
            var d = stream.ReadBytesFromStream(8);

            return new Guid(a, b, c, d);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, TSource value, int startIndex, int count, IEqualityComparer<TSource> comparer = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(startIndex));
            }

            var collection = source as ICollection<TSource> ?? source.ToList();

            if ((uint)startIndex >= (uint)collection.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            comparer ??= EqualityComparer<TSource>.Default;
            var idx = startIndex;

            foreach (var item in collection.Skip(startIndex).TakeWhile((item, index) => index < count))
            {
                if (comparer.Equals(item, value))
                {
                    return idx;
                }

                idx++;
            }

            return -1;
        }
    }

    public static class CollectionExtensions
    {
        private static Random rng = new Random();


        public static int RemoveAll<TSource>(this ICollection<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var removedItems = source.Where(predicate).ToList();

            foreach (var item in removedItems)
            {
                source.Remove(item);
            }

            return removedItems.Count;
        }

        /// <summary>
        ///     Add a range of items to a collection.
        /// </summary>
        /// <typeparam name="T">Type of objects within the collection.</typeparam>
        /// <param name="collection">The collection to add items to.</param>
        /// <param name="items">The items to add to the collection.</param>
        /// <returns>The collection.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     An <see cref="System.ArgumentNullException" /> is thrown if <paramref name="collection" />
        ///     or <paramref name="items" /> is <see langword="null" />.
        /// </exception>
        public static ICollection<T> AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var each in items)
            {
                collection.Add(each);
            }

            return collection;
        }

        public static T RandomElement<T>(this IList<T> list)
        {
            return list[rng.Next(list.Count)];
        }

        public static T RandomElement<T>(this T[] array)
        {
            return array[rng.Next(array.Length)];
        }

        public static bool ContainsAll<T>(this IEnumerable<T> source, IEnumerable<T> values, IEqualityComparer<T> comparer = null)
        {
            return values.All(value => source.Contains(value, comparer));
        }
    }

    public static class ListExtensions
    {
        private static Random random;
        /// <summary> 
        /// Replaces all elements in existing list with the specified values. This does not call OnPropertyChanged.
        /// </summary> 
        public static void ReplaceAll<T>(this ICollection<T> collection, IEnumerable<T> newValues)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (newValues == null) throw new ArgumentNullException(nameof(newValues));

            collection.Clear();
            foreach (var i in newValues) collection.Add(i);
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            if (random == null) random = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}