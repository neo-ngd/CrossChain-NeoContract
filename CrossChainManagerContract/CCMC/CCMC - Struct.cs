using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Neo.SmartContract.Framework;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public struct ToMerkleValue
        {
            public byte[] txHash;
            public BigInteger fromChainID;
        }

        public struct BookKeeper
        {
            public byte[] nextBookKeeper;
            public byte[][] keepers;
        }

        public struct Header
        {
            public BigInteger version;
            public BigInteger chainId;
            public byte[] previousBlockHash;
            public byte[] transactionRoot;
            public byte[] crossStatesRoot;
            public byte[] blockRoot;
            public BigInteger timeStampe;
            public BigInteger height;
            public BigInteger ConsensusData;
            public byte[] consensusPayload;
            public byte[] nextBookKeeper;
        }

        public struct CrossChainTxParameter
        {
            public BigInteger toChainId;
            public byte[] toContract;
            public byte[] method;
            public byte[] args;

            public byte[] txHash;
            public byte[] crossChainId;
            public byte[] fromContract;
        }
    }
}
