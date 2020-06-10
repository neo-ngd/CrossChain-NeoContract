using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Neo.SmartContract.Framework;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public static (byte[], int) ReadBytes(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            return (buffer.Range(offset, count), offset + count);
        }

        public static (BigInteger, int) ReadVarInt(byte[] buffer, int offset)
        {
            byte[] firstByte;
            (firstByte, offset) = ReadBytes(buffer, offset, 1);
            if (firstByte.Equals(new byte[] { 0xFD }))
            {
                return (buffer.Range(offset, 2).ToBigInteger(), offset + 2);
            }
            else if (firstByte.Equals(new byte[] { 0xFE }))
            {
                return (buffer.Range(offset, 4).ToBigInteger(), offset + 4);
            }
            else if (firstByte.Equals(new byte[] { 0XFF }))
            {
                return (buffer.Range(offset, 8).ToBigInteger(), offset + 8);
            }
            else 
            {
                return (firstByte.ToBigInteger(), offset);
            }
        }

        public static (byte[], int) ReadVarBytes(byte[] buffer, int offset) 
        {
            BigInteger count;
            (count, offset) = ReadVarInt(buffer, offset);
            int length = (int)count;
            return ReadBytes(buffer, offset, length);
        }
    }
}
