using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace PecBMS.Helper.Identity
{
    public class IsPecBmsUser : AuthorizationHandler<IsPecBmsUserEnabledRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                       IsPecBmsUserEnabledRequirement requirement)
        {
            if (context.User.HasClaim(f => f.Type == "PecBMSRoute"))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
            return Task.CompletedTask;
        }
    }
    public class IsPecBmsUserEnabledRequirement : IAuthorizationRequirement
    {
    }
}

