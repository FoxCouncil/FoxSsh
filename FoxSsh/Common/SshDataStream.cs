//   !!  // FoxSsh - https://github.com/FoxCouncil/FoxSsh
// *.-". // MIT License
//  | |  // Copyright 2021 The Fox Council

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FoxSsh.Common
{
    public class SshDataStream : IDisposable
    {
        private readonly MemoryStream _memoryStream;

        public long DataAvailable => _memoryStream.Length - _memoryStream.Position;

        public SshDataStream()
        {
            _memoryStream = new MemoryStream(512);
        }

        public SshDataStream(byte[] buffer)
        {
            _memoryStream = new MemoryStream(buffer);
        }

        public void Write(bool value)
        {
            _memoryStream.WriteByte(Convert.ToByte(value));
        }

        public void Write(byte value)
        {
            _memoryStream.WriteByte(value);
        }

        public void Write(uint value)
        {
            var bytes = new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };
            _memoryStream.Write(bytes, 0, 4);
        }

        public void Write(ulong value)
        {
            var bytes = new[] {
                (byte)(value >> 56), (byte)(value >> 48), (byte)(value >> 40), (byte)(value >> 32),
                (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF)
            };

            _memoryStream.Write(bytes, 0, 8);
        }

        public void Write(string str, Encoding encoding)
        {
            var bytes = encoding.GetBytes(str);

            WriteBinary(bytes);
        }

        public void WriteMpInt(byte[] data)
        {
            if (data.Length == 1 && data[0] == 0)
            {
                Write(new byte[4]);
            }
            else
            {
                var length = (uint)data.Length;

                var high = (data[0] & 0x80) != 0;

                if (high)
                {
                    Write(length + 1);
                    Write(0);
                    Write(data);
                }
                else
                {
                    Write(length);
                    Write(data);
                }
            }
        }

        public void Write(byte[] data)
        {
            _memoryStream.Write(data, 0, data.Length);
        }

        public void Write(IEnumerable<byte> data)
        {
            var array = data.ToArray();

            _memoryStream.Write(array, 0, array.Length);
        }

        public void WriteAscii(string data)
        {
            Write(data, Encoding.ASCII);
        }

        public void WriteUtf8(string data)
        {
            Write(data, Encoding.UTF8);
        }

        public void WriteBinary(byte[] buffer)
        {
            Write((uint)buffer.Length);

            _memoryStream.Write(buffer, 0, buffer.Length);
        }

        public void WriteBinary(byte[] buffer, int offset, int count)
        {
            Write((uint)count);

            _memoryStream.Write(buffer, offset, count);
        }

        public bool ReadBoolean()
        {
            var num = _memoryStream.ReadByte();

            if (num == -1)
            {
                throw new EndOfStreamException();
            }

            return num != 0;
        }

        public byte ReadByte()
        {
            var data = ReadBinary(1);

            return data[0];
        }

        public uint ReadUInt32()
        {
            var data = ReadBinary(4);

            return (uint)(data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3]);
        }

        public ulong ReadUInt64()
        {
            var data = ReadBinary(8);

            return (ulong)data[0] << 56 | (ulong)data[1] << 48 | (ulong)data[2] << 40 | (ulong)data[3] << 32 |
                   (ulong)data[4] << 24 | (ulong)data[5] << 16 | (ulong)data[6] << 8 | data[7];
        }

        public string ReadString(Encoding encoding)
        {
            var bytes = ReadBinary();

            return encoding.GetString(bytes);
        }

        public string ReadStringUtf8()
        {
            return ReadString(Encoding.UTF8);
        }

        public string ReadStringAscii()
        {
            return ReadString(Encoding.ASCII);
        }

        public byte[] ReadMpInt()
        {
            var data = ReadBinary();

            if (data.Length == 0)
            {
                return new byte[1];
            }

            if (data[0] != 0)
            {
                return data;
            }

            var output = new byte[data.Length - 1];

            Array.Copy(data, 1, output, 0, output.Length);

            return output;
        }

        public byte[] ReadBinary(int length)
        {
            var data = new byte[length];

            var bytesRead = _memoryStream.Read(data, 0, length);

            if (bytesRead < length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return data;
        }

        public byte[] ReadBinary()
        {
            var length = ReadUInt32();

            return ReadBinary((int)length);
        }

        public byte[] ToByteArray()
        {
            return _memoryStream.ToArray();
        }

        public void Dispose()
        {
            _memoryStream.Dispose();
        }
    }
}