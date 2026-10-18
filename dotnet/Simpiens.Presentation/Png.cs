using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Simpiens.Presentation
{
    /// <summary>
    /// PNG files for render tests and screenshots: 8-bit RGBA, no interlacing. It writes those and reads back what it
    /// wrote (and any PNG of the same form). No image library: zlib comes from <see cref="ZLibStream"/>.
    /// </summary>
    public static class Png
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>Encodes RGBA bytes, row by row from the top.</summary>
        public static byte[] Encode(int width, int height, byte[] rgba)
        {
            if (rgba == null || rgba.Length != width * height * 4) throw new ArgumentException($"expected {width * height * 4} bytes of RGBA.", nameof(rgba));

            using var output = new MemoryStream();
            output.Write(Signature, 0, Signature.Length);

            var header = new byte[13];
            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);
            header[8] = 8;  // bits per channel
            header[9] = 6;  // colour type: RGBA
            WriteChunk(output, "IHDR", header);

            using (var raw = new MemoryStream())
            {
                using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
                {
                    int stride = width * 4;
                    for (int y = 0; y < height; y++)
                    {
                        zlib.WriteByte(0); // filter: none
                        zlib.Write(rgba, y * stride, stride);
                    }
                }
                WriteChunk(output, "IDAT", raw.ToArray());
            }

            WriteChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        public static void Write(string path, int width, int height, byte[] rgba)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, Encode(width, height, rgba));
        }

        /// <summary>Decodes an 8-bit RGBA, non-interlaced PNG into RGBA bytes, row by row from the top.</summary>
        public static byte[] Decode(byte[] png, out int width, out int height)
        {
            if (png == null || png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature)) throw new InvalidDataException("Not a PNG file.");

            width = height = 0;
            using var idat = new MemoryStream();
            int pos = Signature.Length;
            while (pos + 8 <= png.Length)
            {
                int length = (int)ReadBigEndian(png, pos);
                string type = Encoding.ASCII.GetString(png, pos + 4, 4);
                int data = pos + 8;
                if (type == "IHDR")
                {
                    width = (int)ReadBigEndian(png, data);
                    height = (int)ReadBigEndian(png, data + 4);
                    if (png[data + 8] != 8 || png[data + 9] != 6 || png[data + 12] != 0) throw new InvalidDataException("Only 8-bit RGBA, non-interlaced PNG files are supported.");
                }
                else if (type == "IDAT") idat.Write(png, data, length);
                else if (type == "IEND") break;
                pos = data + length + 4; // skip the CRC
            }

            int stride = width * 4;
            var rgba = new byte[height * stride];
            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            var previous = new byte[stride];
            var row = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int filter = zlib.ReadByte();
                zlib.ReadExactly(row, 0, stride);
                Unfilter(filter, row, previous);
                Buffer.BlockCopy(row, 0, rgba, y * stride, stride);
                (previous, row) = (row, previous);
            }
            return rgba;
        }

        private static void Unfilter(int filter, byte[] row, byte[] previous)
        {
            for (int i = 0; i < row.Length; i++)
            {
                int left = i >= 4 ? row[i - 4] : 0;
                int up = previous[i];
                int upLeft = i >= 4 ? previous[i - 4] : 0;
                int add = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter}."),
                };
                row[i] = (byte)(row[i] + add);
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var lengthBytes = new byte[4];
            WriteBigEndian(lengthBytes, 0, (uint)data.Length);
            output.Write(lengthBytes, 0, 4);

            var typeBytes = Encoding.ASCII.GetBytes(type);
            output.Write(typeBytes, 0, 4);
            output.Write(data, 0, data.Length);

            uint crc = Crc32(typeBytes, 0xFFFFFFFFu);
            crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            output.Write(crcBytes, 0, 4);
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] data, uint crc)
        {
            for (int i = 0; i < data.Length; i++) crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint ReadBigEndian(byte[] buffer, int offset)
            => (uint)(buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3]);
    }
}
