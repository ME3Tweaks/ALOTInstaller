using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerCore.ModManager.gamefileformats.sfar
{
    /// <summary>
    /// Basic DLC package. Allows opening and reading the header for files, however you cannot extract or update files
    /// </summary>
    public class DLCPackage
    {
        private const string UNKNOWN_FILENAME = "UNKNOWN";
        public string FileName;
        public struct HeaderStruct
        {
            public uint Magic;
            public uint Version;
            public uint DataOffset;
            public uint EntryOffset;
            public uint FileCount;
            public uint BlockTableOffset;
            public uint MaxBlockSize;
            public string CompressionScheme;
            public void Serialize(Stream stream)
            {
                Magic = stream.ReadUInt32();
                Version = stream.ReadUInt32();
                DataOffset = stream.ReadUInt32();
                EntryOffset = stream.ReadUInt32();
                FileCount = stream.ReadUInt32();
                BlockTableOffset = stream.ReadUInt32();
                MaxBlockSize = stream.ReadUInt32();
                CompressionScheme = stream.ReadStringASCII(4);
                if (Magic != 0x53464152 ||
                    Version != 0x00010000 ||
                    MaxBlockSize != 0x00010000)
                    throw new Exception("This DLC archive format is not supported.");
            }
        }
        [DebuggerDisplay("SFAR FileEntryStruct | {FileName}")]
        public struct FileEntryStruct
        {
            public HeaderStruct Header;
            public uint MyOffset;
            public byte[] Hash;
            public uint BlockSizeIndex;
            public uint UncompressedSize;
            public byte UncompressedSizeAdder;
            public long RealUncompressedSize;
            public uint DataOffset;
            public byte DataOffsetAdder;
            public long RealDataOffset;
            public long BlockTableOffset;
            public long[] BlockOffsets;
            public ushort[] BlockSizes;
            public string FileName;

            public void Serialize(Stream con, HeaderStruct header)
            {
                Header = header;
                MyOffset = (uint)con.Position;
                Hash = con.ReadToBuffer(16);
                BlockSizeIndex = con.ReadUInt32();
                UncompressedSize = con.ReadUInt32();
                UncompressedSizeAdder = (byte)con.ReadByte();
                RealUncompressedSize = UncompressedSize + UncompressedSizeAdder << 32; //does this do anything even??/
                DataOffset = con.ReadUInt32();
                DataOffsetAdder = (byte)con.ReadByte();
                RealDataOffset = DataOffset + DataOffsetAdder << 32;
                if (BlockSizeIndex == 0xFFFFFFFF) //Uncompressed
                {
                    BlockOffsets = new long[1];
                    BlockOffsets[0] = RealDataOffset;
                    BlockSizes = new ushort[1];
                    BlockSizes[0] = (ushort)UncompressedSize;
                    BlockTableOffset = 0;
                }
                else //Compressed
                {

                    int numBlocks = (int)Math.Ceiling(UncompressedSize / (double)header.MaxBlockSize);
                    BlockOffsets = new long[numBlocks];
                    BlockSizes = new ushort[numBlocks];
                    BlockOffsets[0] = RealDataOffset;
                    long pos = con.Position;
                    con.Seek((int)getBlockOffset((int)BlockSizeIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
                    BlockTableOffset = con.Position;
                    BlockSizes[0] = con.ReadUInt16();
                    for (int i = 1; i < numBlocks; i++)
                    {
                        BlockSizes[i] = con.ReadUInt16();
                        BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
                    }
                    con.Seek((int)pos, SeekOrigin.Begin);
                }
            }

            private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
            {
                return entryOffset + (numEntries * 0x1E) + (blockIndex * 2);
            }

        }

        private static readonly byte[] TOCHash = { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C };

        public HeaderStruct Header;
        public FileEntryStruct[] Files;


        public DLCPackage(string filename)
        {
            Load(filename);
        }

        private void Load(string filename)
        {
            this.FileName = filename;
            using FileStream fs = File.OpenRead(FileName);
            Serialize(fs);
        }

        private void Serialize(Stream con)
        {
            Header = new HeaderStruct();
            Header.Serialize(con);
            con.Seek((int)Header.EntryOffset, SeekOrigin.Begin);
            Files = new FileEntryStruct[Header.FileCount];
            for (int i = 0; i < Header.FileCount; i++)
                Files[i].Serialize(con, Header);
            ReadFileNames(con);
        }


        private void ReadFileNames(Stream sfarStream)
        {
            FileEntryStruct entry;
            int fileIndex = -1;
            //Get list of files
            for (int i = 0; i < Header.FileCount; i++)
            {
                entry = Files[i];
                entry.FileName = UNKNOWN_FILENAME;
                Files[i] = entry;
                //find toc
                if (Files[i].Hash.SequenceEqual(TOCHash))
                    fileIndex = i;
            }
            if (fileIndex == -1)
                return;
            MemoryStream m = ReadDecompressedEntry(sfarStream, fileIndex);
            m.Seek(0, 0);
            StreamReader r = new StreamReader(m);
            while (!r.EndOfStream)
            {
                string line = r.ReadLine();
                byte[] hash = ComputeHash(line);
                fileIndex = -1;
                //Match name to hash
                for (int i = 0; i < Header.FileCount; i++)
                {
                    if (Files[i].Hash.SequenceEqual(hash))
                    {
                        fileIndex = i;
                        break;
                    }
                }

                //assign if found
                if (fileIndex != -1)
                {
                    entry = Files[fileIndex];
                    entry.FileName = line;
                    Files[fileIndex] = entry;
                }
            }
        }

        public MemoryStream ReadDecompressedEntry(Stream sfarStream, int index)
        {
            MemoryStream result = new MemoryStream();
            FileEntryStruct e = Files[index];
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                sfarStream.Position = e.DataOffset;
                sfarStream.CopyToEx(result, (int)e.RealUncompressedSize);
                //buff = new byte[e.RealUncompressedSize];
                //fs.Read(buff, 0, buff.Length);
                //result.Write(buff, 0, buff.Length);
            }
            result.Position = 0;
            return result;
        }

        private static byte[] ComputeHash(string input)
        {
            byte[] bytes = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
                bytes[i] = (byte)Sanitize(input[i]);
            var md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(bytes);
        }

        private static char Sanitize(char c)
        {
            switch ((ushort)c)
            {
                case 0x008C: return (char)0x9C;
                case 0x009F: return (char)0xFF;
                case 0x00D0:
                case 0x00DF:
                case 0x00F0:
                case 0x00F7: return c;
            }
            if ((c >= 'A' && c <= 'Z') || (c >= 'À' && c <= 'Þ'))
                return char.ToLowerInvariant(c);
            return c;
        }

        public int FindFileEntry(string fileName)
        {
            return Files.IndexOf(Files.FirstOrDefault(x => x.FileName.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}

