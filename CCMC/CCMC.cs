using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Numerics;

namespace CrossChainContract
{
    public class NeoCrossChainManager : SmartContract
    {
        //Reuqest prefix
        private static readonly byte[] requestIDPrefix = new byte[] { 0x01, 0x01 };
        private static readonly byte[] requestPreifx = new byte[] { 0x01, 0x02 };

        //Header prefix
        private static readonly byte[] latestHeightPrefix = new byte[] { 0x02, 0x01 };
        private static readonly byte[] mCBlockHeadersPrefix = new byte[] { 0x02, 0x02 };
        private static readonly byte[] latestBookKeeperHeightPrefix = new byte[] { 0x02, 0x03 };
        private static readonly byte[] mCKeeperPubKeysPrefix = new byte[] { 0x02, 0x04 };
        private static readonly byte[] mCKeeperHeightPrefix = new byte[] { 0x02, 0x05 };

        //tx prefix
        private static readonly byte[] transactionPrefix = new byte[] { 0x03, 0x01 };

        //代理
        private static readonly byte[] ProxyPrefix = new byte[] { 0x04, 0x01 };

        //常量
        private static readonly int MCCHAIN_PUBKEY_LEN = 67;
        private static readonly int MCCHAIN_SIGNATURE_LEN = 65;
        private static readonly byte[] Operator = "ALsa2JWWsKiMuqZkCpKvZx2iSoBXjNdpZo".ToScriptHash();

        //动态调用
        delegate object DyncCall(string method, object[] args);

        //------------------------------event--------------------------------
        //CrossChainEvent tx.origin, param.txHash, _token, _toChainId, _toContract, rawParam, requestKey
        public static event Action<byte[], byte[], byte[], BigInteger, byte[], byte[], byte[]> CrossChainEvent;
        //CrossChainTxEvent fromChainID, TxParam.toContract, txHash, TxPara.txHash
        public static event Action<BigInteger, byte[], byte[], byte[]> VerifyAndExecuteTxEvent;
        //Sync Genesis Header Event Height, rawHeaders
        public static event Action<BigInteger, byte[]> InitGenesisBlockEvent;
        //更换联盟链公式
        public static event Action<BigInteger, byte[]> ChangeBookKeeperEvent;
        //同步区块头
        public static event Action<BigInteger, byte[]> SyncBlockHeaderEvent;
        public static object Main(string operation, object[] args)
        {
            byte[] caller = ExecutionEngine.CallingScriptHash;
            if (operation == "CrossChain")// 发起跨链交易
            {
                return CrossChain((BigInteger)args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], caller);
            }
            else if (operation == "SyncBlockHeader") //同步区块头
            {
                return SyncBlockHeader((byte[])args[0], (byte[])args[1], (byte[])args[2]);
            }
            else if (operation == "ChangeBookKeeper") // 更新关键区块头公钥
            {
                return ChangeBookKeeper((byte[])args[0], (byte[])args[1], (byte[])args[2]);
            }
            else if (operation == "InitGenesisBlock")// 初始化创世块
            {
                return InitGenesisBlock((byte[])args[0], (byte[])args[1]);
            }
            else if (operation == "VerifyAndExecuteTx")// 执行跨链交易
            {
                return VerifyAndExecuteTx((byte[])args[0], (BigInteger)args[1]);
            }
            else if (operation == "currentSyncHeight")
            {
                return Storage.Get(latestHeightPrefix).AsBigInteger();
            }
            else if (operation == "getHeader")
            {
                return getHeader((BigInteger)args[0]);
            }
            else if (operation == "VerifyAndExecuteTxTest")
            {
                return VerifyAndExecuteTxTest((byte[])args[0], (byte[])args[1]);
            }
            else if (operation == "RegisterProxy")
            {
                return RegisterProxy((byte[])args[0]);
            }
            else if (operation == "ChangeProxyState")
            {
                return ChangeProxyState((byte[])args[0]);
            }
            return false;
        }

        public static bool RegisterProxy(byte[] ProxyScriptHash)
        {
            if (Runtime.CheckWitness(Operator))
            {
                byte[] Proxy = Storage.Get(ProxyPrefix);
                Map<byte[], bool> proxy;
                if (Proxy.AsBigInteger().Equals(0))
                {
                    proxy = new Map<byte[], bool>();
                }
                else
                {
                    proxy = (Map<byte[], bool>)Proxy.Deserialize();
                }
                proxy[ProxyScriptHash] = true;
                Storage.Put(ProxyPrefix, proxy.Serialize());
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool CheckProxyRegisted(byte[] caller)
        {
            byte[] Proxy = Storage.Get(ProxyPrefix);
            if (Proxy.AsBigInteger().Equals(0))
            {
                return false;
            }
            else
            {
                Map<byte[], bool> proxy = (Map<byte[], bool>)Proxy.Deserialize();
                if (proxy.HasKey(caller))
                {
                    return proxy[caller];
                }
                else
                {
                    return false;
                }
            }
        }

        public static bool ChangeProxyState(byte[] ProxyScriptHash)
        {
            if (!Runtime.CheckWitness(Operator)) return false;
            byte[] Proxy = Storage.Get(ProxyPrefix);
            if (Proxy.AsBigInteger().Equals(0))
            {
                return false;
            }
            else
            {
                Map<byte[], bool> proxy = (Map<byte[], bool>)Proxy.Deserialize();
                if (proxy.HasKey(ProxyScriptHash))
                {
                    proxy[ProxyScriptHash] = !proxy[ProxyScriptHash];
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static byte[] getHeader(BigInteger blockHeight)
        {
            byte[] rawHeader = Storage.Get(mCBlockHeadersPrefix.Concat(blockHeight.ToByteArray()));
            if (rawHeader.Equals(new byte[0]))
            {
                return new byte[] { 0x00 };
            }
            else 
            {
                return rawHeader;
            }
        }

        public static bool VerifyAndExecuteTx(byte[] proof, BigInteger blockHeight)
        {
            BigInteger latestHeight = Storage.Get(latestHeightPrefix).AsBigInteger();
            if (blockHeight > latestHeight)
            {
                Runtime.Notify("blockHeight > LatestHeight!");
                return false;
            }
            //根据高度取出root
            byte[] rawHeader = getHeader(blockHeight);
            Header header;
            if (!rawHeader.Equals(new byte[] { 0x00}))
            {
                header = deserializHeader(rawHeader);
            }
            else
            {
                Runtime.Notify("Header does not exist.");
                return false;
            }
            //验证toMerkleValue
            byte[] CrossChainParams = MerkleProve(proof, header.crossStatesRoot);
            if (CrossChainParams.Equals(new byte[] { 0x00 }))
            {
                Runtime.Notify("Proof verify error");
                return false;
            }
            ToMerkleValue merkleValue = deserializMerkleValue(CrossChainParams);
            //通过参数中的txid查看交易是否已经被执行过
            if (Storage.Get(transactionPrefix.Concat(merkleValue.txHash)).AsBigInteger() == 1)
            {
                Runtime.Notify("Transaction has been executed");
                return false;
            }
            //TODO: 确定Neo的跨链ID 并写死(暂定neo id 为4, 后面根据实际情况修改)
            if (merkleValue.TxParam.toChainID != 4)
            {
                Runtime.Notify("Not Neo crosschain tx");
                return false;
            }
            //执行跨链交易
            if (ExecuteCrossChainTx(merkleValue))
            {
                Runtime.Notify("Tx execute success");
            }
            else
            {
                Runtime.Notify("Tx execute fail");
            }

            //发出事件
            VerifyAndExecuteTxEvent
                (merkleValue.fromChainID,
                merkleValue.TxParam.toContract,
                merkleValue.txHash,
                merkleValue.TxParam.txHash);

            return true;
        }

        public static object VerifyAndExecuteTxTest(byte[] proof, byte[] root)
        {
            //验证toMerkleValue
            byte[] CrossChainParams = MerkleProve(proof, root);
            if (CrossChainParams.Equals(new byte[] { 0x00 }))
            {
                Runtime.Notify("Proof verify error");
                return false;
            }
            ToMerkleValue merkleValue = deserializMerkleValue(CrossChainParams);
            //通过参数中的txid查看交易是否已经被执行过
            if (Storage.Get(transactionPrefix.Concat(merkleValue.txHash)).AsBigInteger() == 1)
            {
                Runtime.Notify("Transaction has been executed");
                return false;
            }
            //TODO: 确定Neo的跨链ID 并写死(暂定neo id 为4, 后面根据实际情况修改)
            if (merkleValue.TxParam.toChainID != 4)
            {
                Runtime.Notify("Not Neo crosschain tx");
                return false;
            }
            Runtime.Notify("proof and root are correct");

            return true;
        }

        public static bool CrossChain(BigInteger toChainID, byte[] toChainAddress, byte[] functionName, byte[] args, byte[] caller)
        {
            var tx = (Transaction)ExecutionEngine.ScriptContainer;

            CrossChainTxParameter para = new CrossChainTxParameter
            {
                toChainID = toChainID,
                toContract = toChainAddress,
                method = functionName,
                args = args,

                txHash = tx.Hash,
                //sha256(此合约地址, 交易id)
                crossChainID = SmartContract.Sha256(ExecutionEngine.ExecutingScriptHash.Concat(tx.Hash)),
                fromContract = caller
            };
            var requestId = getRequestID(toChainID);
            var resquestKey = putRequest(toChainID, requestId, para);

            //发出跨链事件
            CrossChainEvent(tx.Hash, para.txHash, caller , para.toChainID, para.toContract, WriteCrossChainTxParameter(para), resquestKey);
            return true;
        }

        public static bool SyncBlockHeader(byte[] rawHeader, byte[] bookKeeper, byte[] signList)
        {
            Header header = deserializHeader(rawHeader);
            //普通区块
            if (header.nextBookKeeper != new Byte[] { })
            {
                Runtime.Notify("Header nextBookKeeper should be empty");
                return false;
            }
            if (header.height < 0)
            {
                Runtime.Notify("block height should > 0");
                return false;
            }

            byte[] key = mCBlockHeadersPrefix;
            key = key.Concat(header.height.AsByteArray());
            byte[] storedRawHeader = Storage.Get(key);
            if (!storedRawHeader.Equals(new byte[0]))
            {
                //表示已经同步过此高度
                return true;
            }
            object[] keepers;
            BigInteger latestBookKeeperHeight = Storage.Get(latestBookKeeperHeightPrefix).AsBigInteger();
            BigInteger targetBlockHeight;
            if (header.height >= latestBookKeeperHeight)
            {
                targetBlockHeight = latestBookKeeperHeight;
            }
            else
            {
                Map<int, BigInteger> MCKeeperHeight = new Map<int, BigInteger>();
                targetBlockHeight = findBookKeeper(MCKeeperHeight.Keys.Length, header.height);
            }
            keepers = (object[])Storage.Get(mCKeeperPubKeysPrefix.Concat(header.height.AsByteArray())).Deserialize();
            int n = keepers.Length;
            int m = n - (n - 1) / 3;
            if (!verifySig(rawHeader, signList, keepers, m))
            {
                Runtime.Notify("Verify header signature failed!");
                return false;
            }
            Storage.Put(mCBlockHeadersPrefix.Concat(header.height.AsByteArray()), rawHeader);
            if (header.height > Storage.Get(latestHeightPrefix).AsBigInteger())
            {
                Storage.Put(latestHeightPrefix, header.height);
            }
            SyncBlockHeaderEvent(header.height, rawHeader);
            return true;
        }

        public static bool ChangeBookKeeper(byte[] rawHeader, byte[] pubKeyList, byte[] signList)
        {
            Header header = deserializHeader(rawHeader);
            if (header.height == 0)
            {
                return InitGenesisBlock(rawHeader, pubKeyList);
            }
            BigInteger latestHeight = Storage.Get(latestHeightPrefix).Concat(new byte[] { 0x00 }).AsBigInteger();
            if (latestHeight > header.height)
            {
                Runtime.Notify("The height of header illegal");
                return false;
            }
            if (header.nextBookKeeper.Length != 20)
            {
                Runtime.Notify("The nextBookKeeper of header is illegal");
                return false;
            }

            BigInteger latestBookKeeperHeight = Storage.Get(latestBookKeeperHeightPrefix).AsBigInteger();
            object[] keepers = (byte[][])Storage.Get(mCKeeperPubKeysPrefix.Concat(latestBookKeeperHeight.ToByteArray())).Deserialize();
            int n = keepers.Length;
            int m = n - (n - 1) / 3;
            if (!verifySig(rawHeader, signList, keepers, m))
            {
                Runtime.Notify("Verify signature failed");
                return false;
            }
            BookKeeper bookKeeper = verifyPubkey(pubKeyList);
            if (header.nextBookKeeper != bookKeeper.nextBookKeeper)
            {
                Runtime.Notify("NextBookers illegal");
                return false;
            }
            //更新最新区块高度
            Storage.Put(latestHeightPrefix, header.height);
            //更新共识公钥高度
            Storage.Put(latestBookKeeperHeightPrefix, header.height);
            //存放完整区块头
            Storage.Put(mCBlockHeadersPrefix.Concat(header.height.AsByteArray()), rawHeader);
            //更新关键区块头高度
            Map<BigInteger, BigInteger> MCKeeperHeight = (Map<BigInteger, BigInteger>)Storage.Get(mCKeeperHeightPrefix).Deserialize();
            MCKeeperHeight[MCKeeperHeight.Keys.Length] = header.height;
            MCKeeperHeight = RemoveOutdateHeight(MCKeeperHeight);
            Storage.Put(mCKeeperHeightPrefix, MCKeeperHeight.Serialize());
            //更新关键区块头公钥
            Storage.Put(mCKeeperPubKeysPrefix, bookKeeper.keepers.Serialize());
            //触发关键区块头事件
            ChangeBookKeeperEvent(header.height, rawHeader);
            return true;
        }

        private static Map<BigInteger, BigInteger> RemoveOutdateHeight(Map<BigInteger, BigInteger> MCKeeperHeight) 
        {
            foreach (BigInteger key in MCKeeperHeight.Keys) 
            {
                if (MCKeeperHeight.Keys.Length > 50)
                {
                    MCKeeperHeight.Remove(key);
                }
                else 
                {
                    break;
                }
            }
            return MCKeeperHeight;
        }


        public static bool InitGenesisBlock(byte[] rawHeader, byte[] pubKeyList)
        {
            if (IsGenesised() != 0) return false;
            Header header = deserializHeader(rawHeader);
            Runtime.Notify("header deserialize");
            if (pubKeyList.Length % MCCHAIN_PUBKEY_LEN != 0)
            {
                Runtime.Notify("Length of pubKeyList is illegal");
                return false;
            }
            BookKeeper bookKeeper = verifyPubkey(pubKeyList);
            Runtime.Notify("header deserialize");
            if (header.nextBookKeeper != bookKeeper.nextBookKeeper)
            {
                Runtime.Notify("NextBookers illegal");
            }
            //更新最新区块高度
            Storage.Put(latestHeightPrefix, header.height);
            //更新共识公钥高度
            Storage.Put(latestBookKeeperHeightPrefix, header.height);
            //更新创世块创建状态 TODO:上线可切换为0
            Storage.Put("IsInitGenesisBlock", 1);
            //存放完整区块头
            Storage.Put(mCBlockHeadersPrefix.Concat(header.height.AsByteArray()), rawHeader);
            //存放关键区块头高度
            Map<BigInteger, BigInteger> MCKeeperHeight = new Map<BigInteger, BigInteger>();
            MCKeeperHeight[0] = header.height;
            Storage.Put(mCKeeperHeightPrefix, MCKeeperHeight.Serialize());
            //存放关键区块头公钥
            Storage.Put(mCKeeperPubKeysPrefix.Concat(header.height.AsByteArray()), bookKeeper.keepers.Serialize());
            //触发创世区块事件
            InitGenesisBlockEvent(header.height, rawHeader);
            return true;
        }

        private static BigInteger findBookKeeper(int mcKeeperHeightLength, BigInteger height)
        {
            if (mcKeeperHeightLength == 1)
            {
                return 0;
            }
            Map<int, BigInteger> MCKeeperHeight = new Map<int, BigInteger>();
            MCKeeperHeight = (Map<int, BigInteger>)Storage.Get(mCKeeperHeightPrefix).Deserialize();
            for (int i = mcKeeperHeightLength; i >= 0; i--)
            {
                if (MCKeeperHeight[i] > height)
                {
                    continue;
                }
                else
                {
                    return MCKeeperHeight[i];
                }
            }
            return 0;
        }

        private static BookKeeper verifyPubkey(byte[] pubKeyList)
        {
            if (pubKeyList.Length % MCCHAIN_PUBKEY_LEN != 0)
            {
                Runtime.Notify("pubKeyList length illegal");
                throw new ArgumentOutOfRangeException();
            }
            int n = pubKeyList.Length / MCCHAIN_PUBKEY_LEN;
            int m = n - (n - 1) / 3;

            return getBookKeeper(n, m, pubKeyList);
        }

        private static BookKeeper getBookKeeper(int keyLength, int m, byte[] pubKeyList)
        {
            byte[] buff = new byte[] { };
            buff = WriteUint16(keyLength, buff);

            byte[][] keepers = new byte[keyLength][];

            for (int i = 0; i < keyLength; i++)
            {
                buff = WriteVarBytes(buff, compressMCPubKey(pubKeyList.Range(i * MCCHAIN_PUBKEY_LEN, MCCHAIN_PUBKEY_LEN)));
                byte[] hash = bytesToBytes32(SmartContract.Sha256((pubKeyList.Range(i * MCCHAIN_PUBKEY_LEN, MCCHAIN_PUBKEY_LEN).Range(3, 64))));
                keepers[i] = hash;
            }
            BookKeeper bookKeeper = new BookKeeper();

            buff = WriteUint16(m, buff);
            bookKeeper.nextBookKeeper = bytesToBytes20(Hash160(buff));
            bookKeeper.keepers = keepers;
            return bookKeeper;
        }

        private static byte[] compressMCPubKey(byte[] key)
        {
            if (key.Length < 34) return key;
            int index = 2;
            byte a = 0x02;
            byte b = 0x03;
            byte[] newkey = key.Range(0, 35);
            byte[] point = key.Range(66, 1);
            if (point.AsBigInteger() % 2 == 0)
            {
                newkey[index] = a;
            }
            else
            {
                newkey[index] = b;
            }
            return newkey;
        }

        private static bool verifySig(byte[] rawHeader, byte[] signList, object[] keepers, int m)
        {
            Runtime.Notify(signList);
            byte[] hash = (SmartContract.Hash256(rawHeader));
            int signed = 0;
            for (int i = 0; i < signList.Length / MCCHAIN_SIGNATURE_LEN; i++)
            {
                byte[] r = (signList.Range(i * MCCHAIN_SIGNATURE_LEN, 32));
                byte[] s = (signList.Range(i * MCCHAIN_SIGNATURE_LEN + 32, 32));
                int index = i * MCCHAIN_SIGNATURE_LEN + 64;
                BigInteger v = signList.Range(index, 1).AsBigInteger();
                byte[] signer;
                if (v == 1)
                {
                    signer = SmartContract.Sha256(Ecrecover(r, s, false, SmartContract.Sha256(hash)));
                }
                else
                {
                    signer = SmartContract.Sha256(Ecrecover(r, s, true, SmartContract.Sha256(hash)));
                }
                Runtime.Notify(signer);
                if (containsAddress(keepers, signer))
                {
                    signed += 1;
                }
            }
            Runtime.Notify(signed);
            return signed >= m;
        }

        private static bool containsAddress(object[] keepers, byte[] pubkey)
        {
            for (int i = 0; i < keepers.Length; i++)
            {
                if (keepers[i].Equals(pubkey))
                {
                    return true;
                }
            }
            return false;
        }

        private static BigInteger IsGenesised()
        {
            return Storage.Get("IsInitGenesisBlock").AsBigInteger();
        }

        private static Header deserializHeader(byte[] Source)
        {
            Header header = new Header();
            int offset = 0;
            //获取version
            header.version = Source.Range(offset, 4).ToBigInteger();
            offset += 4;
            //获取chainID
            header.chainId = Source.Range(offset, 8).ToBigInteger();
            offset += 8;
            //获取prevBlockHash, Hash
            header.prevBlockHash = ReadHash(Source, offset);
            offset += 32;
            //获取transactionRoot, Hash
            header.transactionRoot = ReadHash(Source, offset);
            offset += 32;
            //获取crossStatesRoot, Hash
            header.crossStatesRoot = ReadHash(Source, offset);
            offset += 32;
            //获取blockRoot, Hash
            header.blockRoot = ReadHash(Source, offset);
            offset += 32;
            //获取timeStamp,uint32
            header.timeStamp = Source.Range(offset, 4).ToBigInteger();
            offset += 4;
            //获取height
            header.height = Source.Range(offset, 4).ToBigInteger();
            offset += 4;
            //获取consensusData
            header.ConsensusData = Source.Range(offset, 8).ToBigInteger();
            offset += 8;
            //获取consensysPayload
            var temp = ReadVarBytes(Source, offset);
            header.consensusPayload = (byte[])temp[0];
            offset = (int)temp[1];
            //获取nextBookKeeper
            header.nextBookKeeper = Source.Range(offset, 20);

            return header;
        }

        private static bool ExecuteCrossChainTx(ToMerkleValue value)
        {
            if (value.TxParam.toContract.Length == 20)
            {
                DyncCall TargetContract = (DyncCall)value.TxParam.toContract.ToDelegate();
                object[] parameter = new object[] { value.TxParam.args,  value.TxParam.fromContract, value.fromChainID};
                if (TargetContract(value.TxParam.method.AsString(), parameter) is null)
                {
                    return false;
                }
                else
                {
                    Storage.Put(transactionPrefix.Concat(value.txHash), 1);
                    return true;
                }
            }
            else
            {
                Runtime.Notify("Contract length is not correct");
                return false;
            }
        }

        private static BigInteger getRequestID(BigInteger chainID)
        {
            byte[] requestID = Storage.Get(requestIDPrefix.Concat(chainID.ToByteArray()));
            if (requestID != null)
            {
                return requestID.AsBigInteger();
            }
            return 0;
        }

        private static byte[] putRequest(BigInteger chainID, BigInteger requestID, CrossChainTxParameter para)
        {
            requestID = requestID + 1;
            byte[] requestKey = requestPreifx.Concat(chainID.ToByteArray()).Concat(requestID.ToByteArray());
            Storage.Put(requestKey, WriteCrossChainTxParameter(para));
            Storage.Put(requestIDPrefix.Concat(chainID.ToByteArray()), requestID);
            return requestKey;
        }

        private static ToMerkleValue deserializMerkleValue(byte[] Source)
        {
            ToMerkleValue result = new ToMerkleValue();
            int offset = 0;

            //获取txHash
            var temp = ReadVarBytes(Source, offset);
            result.txHash = (byte[])temp[0];
            offset = (int)temp[1];

            //获取fromChainID, Uint64
            result.fromChainID = Source.Range(offset, 8).ToBigInteger();
            offset = offset + 8;

            //获取CrossChainTxParameter
            result.TxParam = deserializCrossChainTxParameter(Source, offset);
            return result;
        }

        private static CrossChainTxParameter deserializCrossChainTxParameter(byte[] Source, int offset)
        {
            CrossChainTxParameter txParameter = new CrossChainTxParameter();
            //获取txHash
            var temp = ReadVarBytes(Source, offset);
            txParameter.txHash = (byte[])temp[0];
            offset = (int)temp[1];

            //获取crossChainId
            temp = ReadVarBytes(Source, offset);
            txParameter.crossChainID = (byte[])temp[0];
            offset = (int)temp[1];

            //获取fromContract
            temp = ReadVarBytes(Source, offset);
            txParameter.fromContract = (byte[])temp[0];
            offset = (int)temp[1];

            //获取toChainID
            txParameter.toChainID = Source.Range(offset, 8).ToBigInteger();
            offset = offset + 8;

            //获取toContract
            temp = ReadVarBytes(Source, offset);
            txParameter.toContract = (byte[])temp[0];
            offset = (int)temp[1];

            //获取method
            temp = ReadVarBytes(Source, offset);
            txParameter.method = (byte[])temp[0];
            offset = (int)temp[1];

            //获取参数
            temp = ReadVarBytes(Source, offset);
            txParameter.args = (byte[])temp[0];
            offset = (int)temp[1];

            return txParameter;
        }

        private static byte[] WriteCrossChainTxParameter(CrossChainTxParameter para)
        {
            byte[] result = new byte[] { };
            result = WriteVarBytes(result, para.txHash);
            result = WriteVarBytes(result, para.crossChainID);
            result = WriteVarBytes(result, para.fromContract);
            //write Uint64
            byte[] toChainIDBytes = PadRight(para.toChainID.AsByteArray(), 8);
            result = result.Concat(toChainIDBytes);
            result = WriteVarBytes(result, para.toContract);
            result = WriteVarBytes(result, para.method);
            result = WriteVarBytes(result, para.args);
            return result;
        }

        private static byte[] MerkleProve(byte[] path, byte[] root)
        {
            int offSet = 0;
            var temp = ReadVarBytes(path, offSet);
            byte[] value = (byte[])temp[0];
            offSet = (int)temp[1];
            byte[] hash = HashLeaf(value);
            int size = (path.Length - offSet) / 32;
            for (int i = 0; i < size; i++)
            {
                var f = ReadBytes(path, offSet, 1);
                offSet = (int)f[1];

                var v = ReadBytes(path, offSet, 32);
                offSet = (int)v[1];
                if ((byte[])f[0] == new byte[] { 0 })
                {
                    hash = HashChildren((byte[])v[0], hash);
                }
                else
                {
                    hash = HashChildren(hash, (byte[])v[0]);
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

        private static byte[] HashChildren(byte[] v, byte[] hash)
        {
            byte[] prefix = { 1 };
            return SmartContract.Sha256(prefix.Concat(v).Concat(hash));
        }

        private static byte[] HashLeaf(byte[] value)
        {
            byte[] prefix = { 0x00 };
            return SmartContract.Sha256(prefix.Concat(value));
        }

        public static object[] DeserializeArgs(byte[] buffer)
        {
            var offset = 0;
            var res = ReadVarBytes(buffer, offset);
            var assetAddress = res[0];

            res = ReadVarBytes(buffer, (int)res[1]);
            var toAddress = res[0];

            res = ReadVarInt(buffer, (int)res[1]);
            var amount = res[0];

            return new object[] { assetAddress, toAddress, amount };
        }

        private static byte[] WriteUint16(BigInteger value, byte[] Source)
        {
            return Source.Concat(PadRight(value.ToByteArray(), 2));
        }

        private static byte[] WriteVarBytes(byte[] Source, byte[] Target)
        {
            return WriteVarInt(Target.Length, Source).Concat(Target);
        }

        private static byte[] WriteVarInt(BigInteger value, byte[] Source)
        {
            if (value < 0)
            {
                return Source;
            }
            else if (value < 0xFD)
            {
                return Source.Concat(value.ToByteArray());
            }
            else if (value <= 0xFFFF) // 0xff, need to pad 1 0x00
            {
                byte[] length = new byte[] { 0xFD };
                var v = PadRight(value.ToByteArray(), 2);
                return Source.Concat(length).Concat(v);
            }
            else if (value <= 0XFFFFFFFF) //0xffffff, need to pad 1 0x00 
            {
                byte[] length = new byte[] { 0xFE };
                var v = PadRight(value.ToByteArray(), 4);
                return Source.Concat(length).Concat(v);
            }
            else //0x ff ff ff ff ff, need to pad 3 0x00
            {
                byte[] length = new byte[] { 0xFF };
                var v = PadRight(value.ToByteArray(), 8);
                return Source.Concat(length).Concat(v);
            }
        }

        private static byte[] ReadHash(byte[] Source, int offset)
        {
            if (offset + 32 <= Source.Length)
            {
                return Source.Range(offset, 32);
            }
            throw new ArgumentOutOfRangeException();
        }

        private static object[] ReadVarInt(byte[] buffer, int offset)
        {
            var res = ReadBytes(buffer, offset, 1); // read the first byte
            var fb = (byte[])res[0];
            if (fb.Length != 1) throw new ArgumentOutOfRangeException();
            var newOffset = (int)res[1];
            if (fb.Equals(new byte[] { 0xFD }))
            {
                return new object[] { buffer.Range(newOffset, 2).ToBigInteger(), newOffset + 2 };
            }
            else if (fb.Equals(new byte[] { 0xFE }))
            {
                return new object[] { buffer.Range(newOffset, 4).ToBigInteger(), newOffset + 4 };
            }
            else if (fb.Equals(new byte[] { 0xFF }))
            {
                return new object[] { buffer.Range(newOffset, 8).ToBigInteger(), newOffset + 8 };
            }
            else
            {
                return new object[] { fb.Concat(new byte[] { 0x00 }).ToBigInteger(), newOffset };
            }
        }

        private static object[] ReadVarBytes(byte[] buffer, int offset)
        {
            var res = ReadVarInt(buffer, offset);
            var count = (int)res[0];
            var newOffset = (int)res[1];
            return ReadBytes(buffer, newOffset, count);
        }

        private static object[] ReadBytes(byte[] buffer, int offset, int count)
        {
            if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            return new object[] { buffer.Range(offset, count), offset + count };
        }

        private static byte[] PadRight(byte[] value, int length)
        {
            var l = value.Length;
            if (l > length)
                return value;
            for (int i = 0; i < length - l; i++)
            {
                value = value.Concat(new byte[] { 0x00 });
            }
            return value;
        }

        private static byte[] bytesToBytes20(byte[] Source)
        {
            if (Source.Length != 20)
            {
                throw new ArgumentOutOfRangeException();
            }
            else
            {
                return Source;
            }
        }

        private static byte[] bytesToBytes32(byte[] Source)
        {
            if (Source.Length != 32)
            {
                throw new ArgumentOutOfRangeException();
            }
            else
            {
                return Source;
            }
        }

        [Syscall("Neo.Cryptography.Ecrecover")]
        public static extern byte[] Ecrecover(byte[] r, byte[] s, bool v, byte[] message);
    }
    public struct ToMerkleValue
    {
        public byte[] txHash;
        public BigInteger fromChainID;
        public CrossChainTxParameter TxParam;
    }

    public struct CrossChainTxParameter
    {
        public BigInteger toChainID;
        public byte[] toContract;
        public byte[] method;
        public byte[] args;

        public byte[] txHash;
        public byte[] crossChainID;
        public byte[] fromContract;
    }

    public struct Header
    {
        public BigInteger version;//uint32
        public BigInteger chainId;//uint64
        public byte[] prevBlockHash;//Hash
        public byte[] transactionRoot;//Hash
        public byte[] crossStatesRoot;//Hash
        public byte[] blockRoot;//Hash
        public BigInteger timeStamp;//uint32
        public BigInteger height;//uint32
        public BigInteger ConsensusData;//uint64
        public byte[] consensusPayload;
        public byte[] nextBookKeeper;
    }

    public struct BookKeeper
    {
        public byte[] nextBookKeeper;
        public byte[][] keepers;
    }


}
