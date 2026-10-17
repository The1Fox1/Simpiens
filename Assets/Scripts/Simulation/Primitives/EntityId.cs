using System;

namespace Simpiens
{
    /// <summary>
    /// The id of a simulation entity: an agent, a resource node, a stall, a hearth, a bunk (Epic CORE C3). Engine-free,
    /// and bit-compatible with <c>UnityEngine.GUID</c>, which it replaced: the same four 32-bit words in the same order,
    /// the same ordering, hash code and hex text. Ids feed sort orders, <c>SimRandom</c> streams (through the hash code),
    /// dictionary layouts and saves (as raw memory), so keeping all of them makes the swap change no behaviour and keeps
    /// existing saves readable. Lives in the root namespace so it wins over Unity's own <c>UnityEngine.EntityId</c> in
    /// every <c>Simpiens.*</c> file.
    /// </summary>
    [Serializable]
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>, IComparable
    {
        // Same layout as UnityEngine.GUID (m_Value0..m_Value3): unmanaged records holding ids are saved as raw memory
        private readonly uint _value0;
        private readonly uint _value1;
        private readonly uint _value2;
        private readonly uint _value3;

        public EntityId(uint a, uint b, uint c, uint d)
        {
            _value0 = a;
            _value1 = b;
            _value2 = c;
            _value3 = d;
        }

        /// <summary>Parses the 32-digit hex text <see cref="ToString"/> writes; anything else gives the empty id.</summary>
        public EntityId(string hex)
        {
            this = TryParse(hex, out var id) ? id : default;
        }

        /// <summary>The empty id (all zero): "no entity".</summary>
        public static readonly EntityId None = default;

        public bool IsEmpty => _value0 == 0 && _value1 == 0 && _value2 == 0 && _value3 == 0;

        /// <summary>A random id, outside the simulation's seeded sequence (tests, editor tools). Never use for simulation entities.</summary>
        public static EntityId Generate()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            return new EntityId(BitConverter.ToUInt32(bytes, 0), BitConverter.ToUInt32(bytes, 4), BitConverter.ToUInt32(bytes, 8), BitConverter.ToUInt32(bytes, 12));
        }

        public static bool operator ==(EntityId x, EntityId y)
        {
            return x._value0 == y._value0 && x._value1 == y._value1 && x._value2 == y._value2 && x._value3 == y._value3;
        }

        public static bool operator !=(EntityId x, EntityId y) => !(x == y);

        public static bool operator <(EntityId x, EntityId y)
        {
            if (x._value0 != y._value0) return x._value0 < y._value0;
            if (x._value1 != y._value1) return x._value1 < y._value1;
            if (x._value2 != y._value2) return x._value2 < y._value2;
            return x._value3 < y._value3;
        }

        public static bool operator >(EntityId x, EntityId y)
        {
            if (x < y) return false;
            return x != y;
        }

        public bool Equals(EntityId other) => this == other;

        public override bool Equals(object obj) => obj is EntityId other && this == other;

        /// <summary>Unity's GUID hash, kept exactly: <c>SimRandom</c> streams are keyed on it.</summary>
        public override int GetHashCode()
        {
            return (int)((((((_value0 * 397) ^ _value1) * 397) ^ _value2) * 397) ^ _value3);
        }

        public int CompareTo(EntityId other)
        {
            if (this < other) return -1;
            if (this > other) return 1;
            return 0;
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;
            return CompareTo((EntityId)obj);
        }

        private const string HexDigits = "0123456789abcdef";

        /// <summary>
        /// 32 lowercase hex digits, as Unity wrote GUIDs: word by word, each word's nibbles least significant first.
        /// </summary>
        public override string ToString()
        {
            var chars = new char[32];
            Write(chars, 0, _value0);
            Write(chars, 8, _value1);
            Write(chars, 16, _value2);
            Write(chars, 24, _value3);
            return new string(chars);
        }

        private static void Write(char[] chars, int offset, uint word)
        {
            for (int j = 0; j < 8; j++)
            {
                chars[offset + j] = HexDigits[(int)((word >> (j * 4)) & 0xF)];
            }
        }

        /// <summary>Parses <see cref="ToString"/>'s text. False, with the empty id, for anything else or for the empty id.</summary>
        public static bool TryParse(string hex, out EntityId result)
        {
            result = default;
            if (hex == null || hex.Length != 32) return false;
            uint a = 0, b = 0, c = 0, d = 0;
            for (int i = 0; i < 32; i++)
            {
                int nibble = Nibble(hex[i]);
                if (nibble < 0) return false;
                uint shifted = (uint)nibble << ((i % 8) * 4);
                switch (i / 8)
                {
                    case 0: a |= shifted; break;
                    case 1: b |= shifted; break;
                    case 2: c |= shifted; break;
                    default: d |= shifted; break;
                }
            }
            result = new EntityId(a, b, c, d);
            return !result.IsEmpty;
        }

        private static int Nibble(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
            if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
            return -1;
        }
    }
}
