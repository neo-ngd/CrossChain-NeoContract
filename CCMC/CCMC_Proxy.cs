using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;

namespace CCMC_Proxy
{
    public class CCMC_Proxy : SmartContract
    {
        private delegate object Dyncall(string method, object[] args);

        [DisplayName("error")]
        public static event Action<string> Error;

        private static readonly string mapName = "contract";

        private static readonly byte[] InitialAdminScriptHash = "AJhZmdHxW44FWMiMxD5bTiF7UgHcp3g2Fr".ToScriptHash();

        public static object Main(string method, object[] args)
        {
            if (method == "getProxyAdmin") return GetAdmin();
            if (method == "getRedirect") return GetRedirect();
            if (method == "proxyName") return Name();
            if (method == "setProxyAdmin") return SetAdmin((byte[])args[0]);
            if (method == "setRedirect") return SetRedirect((byte[])args[0]);

            return ((Dyncall)GetRedirect().ToDelegate())(method, args);
        }

        [DisplayName("getProxyAdmin")]
        public static byte[] GetAdmin()
        {
            StorageMap map = Storage.CurrentContext.CreateMap(mapName);
            var storageAdmin = map.Get("admin");

            if (storageAdmin.Length == 0)
                return InitialAdminScriptHash;
            return storageAdmin;
        }

        [DisplayName("getRedirect")]
        public static byte[] GetRedirect() => Storage.CurrentContext.CreateMap(mapName).Get("redirect");

        private static bool IsAdmin() => Runtime.CheckWitness(GetAdmin());

        [DisplayName("proxyName")]
        public static string Name() => "CCMC-Proxy";

        [DisplayName("setProxyAdmin")]
        public static bool SetAdmin(byte[] account)
        {
            if (!IsAdmin())
            {
                Error("No authorization.");
                return false;
            }

            StorageMap map = Storage.CurrentContext.CreateMap(mapName);
            map.Put("admin", account);
            return true;
        }

        [DisplayName("setRedirect")]
        public static bool SetRedirect(byte[] scriptHash)
        {
            if (!IsAdmin())
            {
                Error("No authorization.");
                return false;
            }

            StorageMap contract = Storage.CurrentContext.CreateMap(mapName);
            contract.Put("redirect", scriptHash);
            return true;
        }
    }
}
