using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    /// <summary>
    /// Represents a Bluetooth device address
    /// </summary>
    public sealed class Bdaddr
    {
        private readonly byte[] _bytes;

        /// <summary>
        /// Construct a Bdaddr from a string of the form "xx:xx:xx:xx:xx:xx".
        /// </summary>
        /// <param name="addr">Bluetooth device address</param>
        public Bdaddr(string addr)
        {
            _bytes = new byte[6];
            _bytes[5] = Convert.ToByte(addr.Substring(0, 2), 16);
            _bytes[4] = Convert.ToByte(addr.Substring(3, 2), 16);
            _bytes[3] = Convert.ToByte(addr.Substring(6, 2), 16);
            _bytes[2] = Convert.ToByte(addr.Substring(9, 2), 16);
            _bytes[1] = Convert.ToByte(addr.Substring(12, 2), 16);
            _bytes[0] = Convert.ToByte(addr.Substring(15, 2), 16);
        }

        internal Bdaddr(BinaryReader reader)
        {
            _bytes = reader.ReadBytes(6);
            if (_bytes.Length != 6)
            {
                throw new EndOfStreamException();
            }
        }

        internal void WriteBytes(BinaryWriter writer)
        {
            writer.Write(_bytes);
        }

        /// <summary>
        /// The string representation of a Bluetooth device address (xx:xx:xx:xx:xx:xx)
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            return String.Format("{0:x2}:{1:x2}:{2:x2}:{3:x2}:{4:x2}:{5:x2}", _bytes[5], _bytes[4], _bytes[3], _bytes[2], _bytes[1], _bytes[0]);
        }

        /// <summary>
        /// Gets a hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return _bytes[0] ^ (_bytes[1] << 8) ^ (_bytes[2] << 16) ^ (_bytes[3] << 24) ^ (_bytes[4] & 0xff) ^ (_bytes[5] << 8);
        }

        /// <summary>
        /// Equals
        /// </summary>
        /// <param name="obj">Other object</param>
        /// <returns>Result</returns>
        public override bool Equals(object obj)
        {
            var bdaddrObj = obj as Bdaddr;
            if ((object)bdaddrObj == null)
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return _bytes.SequenceEqual(((Bdaddr)obj)._bytes);
        }

        /// <summary>
        /// Equality check
        /// </summary>
        /// <param name="a">First Bdaddr</param>
        /// <param name="b">Second Bdaddr</param>
        /// <returns>Result</returns>
        public static bool operator ==(Bdaddr a, Bdaddr b)
        {
            if ((object)a == null)
            {
                return (object)b == null;
            }
            return a.Equals(b);
        }

        /// <summary>
        /// Inequality check
        /// </summary>
        /// <param name="a">First Bdaddr</param>
        /// <param name="b">Second Bdaddr</param>
        /// <returns>Result</returns>
        public static bool operator !=(Bdaddr a, Bdaddr b)
        {
            return !(a == b);
        }
    }
}
