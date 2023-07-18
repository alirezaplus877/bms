using PEC.CoreCommon.Security.Encryptor;
using PecBMS.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PecBMS.Helper.Identity
{
    public static class IdentityExtensions
    {
        public static string GetClient_Id(this ClaimsPrincipal identity)
        {
            try
            {
                Claim claim = identity?.FindFirst(CustomClaimTypes.client_Id);
                if (claim != null)
                {
                    return claim.Value;
                }
                return "";
            }
            catch (Exception)
            {

                return "";
            }
        }

        public static long GetUserId(this ClaimsPrincipal identity)
        {
            try
            {
                Claim claim = identity?.FindFirst(CustomClaimTypes.userId);
                if (claim != null)
                {
                    return long.Parse(claim.Value);
                }
                return 0;
            }
            catch (Exception)
            {

                return 0;
            }
        }
        public static bool WithClaim(this ClaimsPrincipal identity, string ClaimValue, string ClaimType)
        {
            return identity.Claims.Any(a => a.Value == ClaimValue);

        }
        public static List<WalletsInfo> GetWallets(this ClaimsPrincipal identity)
        {
            List<WalletsInfo> returnValue = new List<WalletsInfo>();
            try
            {
                List<Claim> claims = identity?.FindAll(CustomClaimTypes.pecBMSWallet).ToList();

                Encryptor ecn = new Encryptor();
                foreach (var item in claims)
                {
                    WalletsInfo inputDto = new WalletsInfo();
                    string walletinfo = ecn.Decrypt(item.Value);
                    var items = walletinfo.Split(':');
                    inputDto.WalletCode = items[0];
                    inputDto.CorporationPIN = items[1];
                    returnValue.Add(inputDto);
                }
                return returnValue;
            }
            catch (Exception)
            {

                return returnValue;
            }
        }

        public static bool IsWalletOwner(this ClaimsPrincipal identity, string walletCode)
        {
            var items = GetWallets(identity);
            var isOwner = items.Any(p => p.WalletCode.ToString() == walletCode);
            return isOwner;
        }
    }
}
