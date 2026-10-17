using System;
using System.Buffers.Binary;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// A session save as a file (Milestone 1 save/load): a fixed header in plain bytes, then the serialized
    /// <see cref="SessionSave"/>. The header holds the magic "SIMPSAVE", the save format version, the payload's length
    /// and its FNV-1a checksum (little-endian), so a load checks the format, the length and the checksum before the
    /// serializer reads anything. A save from another format, a cut-off file or a damaged one is refused with a reason
    /// instead of being loaded wrongly (D4).
    /// </summary>
    public static class SaveFile
    {
        public const string Extension = ".simsave";
        public const int HeaderSize = 24;

        private static readonly byte[] Magic = { (byte)'S', (byte)'I', (byte)'M', (byte)'P', (byte)'S', (byte)'A', (byte)'V', (byte)'E' };

        public static byte[] Encode(SessionSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            byte[] payload = SaveSerializer.Serialize(save);
            var file = new byte[HeaderSize + payload.Length];
            Magic.CopyTo(file, 0);
            var span = file.AsSpan();
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), SessionSaveHeader.CurrentFormatVersion);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12, 4), payload.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(16, 8), Checksum(payload));
            payload.CopyTo(file, HeaderSize);
            return file;
        }

        /// <summary>Reads a save file, or explains (in player-facing text) why it can't be read.</summary>
        public static bool TryDecode(ReadOnlySpan<byte> file, out SessionSave save, out string refusal)
        {
            save = null;
            if (file.Length < HeaderSize || !file.Slice(0, Magic.Length).SequenceEqual(Magic))
            {
                refusal = "The file is not a Simpiens save.";
                return false;
            }

            int version = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(8, 4));
            if (version != SessionSaveHeader.CurrentFormatVersion)
            {
                refusal = $"The save was made by another version of the game (save format {version}; this game reads format {SessionSaveHeader.CurrentFormatVersion}).";
                return false;
            }

            int length = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(12, 4));
            if (length <= 0 || length != file.Length - HeaderSize)
            {
                refusal = $"The save is incomplete: it should hold {length} bytes of game but holds {file.Length - HeaderSize}.";
                return false;
            }

            var payload = file.Slice(HeaderSize);
            if (Checksum(payload) != BinaryPrimitives.ReadUInt64LittleEndian(file.Slice(16, 8)))
            {
                refusal = "The save is damaged: its contents don't match its checksum.";
                return false;
            }

            try
            {
                save = SaveSerializer.Deserialize<SessionSave>(payload);
            }
            catch (Exception e)
            {
                refusal = $"The save is damaged and can't be read ({e.GetType().Name}).";
                return false;
            }

            if (save?.Header == null)
            {
                save = null;
                refusal = "The save is damaged: it has no header.";
                return false;
            }
            refusal = null;
            return true;
        }

        /// <summary>FNV-1a, 64-bit.</summary>
        public static ulong Checksum(ReadOnlySpan<byte> bytes)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= 1099511628211UL;
            }
            return h;
        }
    }
}
