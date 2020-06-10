using System;
using System.Collections.Generic;
using System.Text;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public static byte[] HashChildren(byte[] v, byte[] hash)
        {
            byte[] prefix = { 1 };
            return Crypto.SHA256(prefix.Concat(v).Concat(hash));
        }

        public static byte[] HashLeaf(byte[] value)
        {
            byte[] prefix = { 0x00 };
            return Crypto.SHA256(prefix.Concat(value));
        }

        public static byte[] MerkleProve(byte[] path, byte[] root)
        {
            int offset = 0;
            byte[] value;
            (value, offset) = ReadVarBytes(path, offset);
            byte[] hash = HashLeaf(value);
            int size = (path.Length - offset) / 32;
            for (int i = 0; i < size; i++)
            {
                byte[] prefixBytes;
                byte[] valueBytes;
                (prefixBytes, offset) = ReadBytes(path, offset, 1);
                (valueBytes, offset) = ReadBytes(path, offset, 32);
                if (prefixBytes.Equals(new byte[] { 0 }))
                {
                    hash = HashChildren(valueBytes, hash);
                }
                else
                {
                    hash = HashChildren(hash, valueBytes);
                }

            }
            if (hash.Equals(root))
            {
                return value;
            }
            else
            {
                return new byte[] { 0x00 };
            }
        }
    }
}
