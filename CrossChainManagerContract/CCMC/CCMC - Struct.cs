using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace CrossChainManagerContract.CCMC
{
    public partial class CCMC : SmartContract
    {
        public struct ToMerkleValue
        {
            public byte[] txHash;
            public BigInteger fromChainID;
            public CrossChainTxParameter TxParam;
        }

        public static ToMerkleValue deserializeMerkleValue(byte[] source)
        {
            ToMerkleValue result = new ToMerkleValue();
            int offset = 0;

            (result.txHash, offset) = ReadVarBytes(source, offset);
            result.fromChainID = source.Range(offset, 8).ToBigInteger();
            offset += 8;
            result.TxParam = deserializeCrossChainTxParameter(source, offset);
            return result;
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

        public static Header deserializeHeader(byte[] source)
        {
            Header header = new Header();
            int offset = 0;
            try
            {
                //get version
                header.version = source.Range(offset, 4).ToBigInteger();
                offset += 4;

                //get chainID
                header.chainId = source.Range(offset, 8).ToBigInteger();

                //get prevBlockHash
                (header.previousBlockHash, offset) = ReadHash(source, offset);

                //get transactionRoot Hash
                (header.transactionRoot, offset) = ReadHash(source, offset);

                //get crossStatesRoot, Hash
                (header.crossStatesRoot, offset) = ReadHash(source, offset);

                //get blockRoot, Hash
                (header.blockRoot, offset) = ReadHash(source, offset);

                //get timeStamp
                header.timeStampe = source.Range(offset, 4).ToBigInteger();
                offset += 4;

                //get height
                header.height = source.Range(offset, 4).ToBigInteger();
                offset += 4;

                //get consensusData
                header.ConsensusData = source.Range(offset, 8).ToBigInteger();
                offset += 8;

                //get consensusPayload
                (header.consensusPayload, offset) = ReadVarBytes(source, offset);

                //get nextBookKeeper
                header.nextBookKeeper = source.Range(offset, 20);

                return header;
            }
            catch
            {
                Runtime.Notify("deserializeHeader failed");
                throw new Exception();
            }
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

        public static byte[] WriteCrossChainTxParameter(CrossChainTxParameter para)
        {
            byte[] result = new byte[] { };
            result = WriteVarBytes(result, para.txHash);
            result = WriteVarBytes(result, para.crossChainId);
            result = WriteVarBytes(result, para.fromContract);
            result = WriteVarBytes(result, PadRight(para.toChainId.ToByteArray(), 8));
            result = WriteVarBytes(result, para.toContract);
            result = WriteVarBytes(result, para.method);
            result = WriteVarBytes(result, para.args);
            return result;
        }

        public static CrossChainTxParameter deserializeCrossChainTxParameter(byte[] source, int offset)
        {
            CrossChainTxParameter txParameter = new CrossChainTxParameter();
            //get txHash
            (txParameter.txHash, offset) = ReadVarBytes(source, offset);

            //get crossChainId
            (txParameter.crossChainId, offset) = ReadVarBytes(source, offset);

            //get fromContract
            (txParameter.fromContract, offset) = ReadVarBytes(source, offset);

            //get toChainID
            txParameter.toChainId = source.Range(offset, 8).ToBigInteger();
            offset += 8;

            //get toContract
            (txParameter.toContract, offset) = ReadVarBytes(source, offset);

            //get method
            (txParameter.method, offset) = ReadVarBytes(source, offset);

            //get method parameters
            (txParameter.args, offset) = ReadVarBytes(source, offset);
            return txParameter;
        }
    }
}
