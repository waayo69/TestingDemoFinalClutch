using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

public class BaseController : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Check for [AllowAnonymous] on the action or controller
        var hasAllowAnonymous =
            context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute);

        if (!hasAllowAnonymous && !User.Identity.IsAuthenticated)
        {
            context.Result = RedirectToAction("Login", "Account");
        }
        base.OnActionExecuting(context);
    }
}
