using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PecBMS.Helper.Identity
{
    public class CustomClaimTypes
    {
        public static readonly string userId = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
        public static readonly string userName = "userName";
        public static readonly string nationalCode = "nationalCode";
        public static readonly string pecBMSWallet = "PecBMSWallet";
        public static readonly string client_Id = "client_Id";
        public static string Client_Id { get; set; }
        public static long UserId { get; set; }
        public static string UserName { get; set; }
        public static string NationalCode { get; set; }
        public static string BMSWalet { get; set; }
    }
}
