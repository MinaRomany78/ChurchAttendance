using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChurchAttendance.Security;

/// <summary>
/// Global resource filter. For any action carrying [RequireStageSelected] (directly or via its controller),
/// blocks the request unless ICurrentStageAccessor has a valid, currently-active stage in session. This is the
/// server-side enforcement: a user cannot reach stage-scoped data or actions by guessing a URL, because this
/// runs before the action executes regardless of what the UI shows.
/// </summary>
public class StageContextFilter : IAsyncResourceFilter
{
    private readonly ICurrentStageAccessor _stage;

    public StageContextFilter(ICurrentStageAccessor stage) => _stage = stage;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var required = descriptor.MethodInfo.GetCustomAttributes(typeof(RequireStageSelectedAttribute), true).Length > 0
                           || descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(RequireStageSelectedAttribute), true).Length > 0;

            if (required && !await _stage.IsValidAsync())
            {
                var request = context.HttpContext.Request;
                var isAjax = request.Headers.XRequestedWith == "XMLHttpRequest"
                             || request.Headers.Accept.Any(a => a is not null && a.Contains("application/json"));

                context.Result = isAjax
                    ? new ObjectResult(new { success = false, message = "يرجى اختيار المرحلة الدراسية أولًا." }) { StatusCode = StatusCodes.Status409Conflict }
                    : new RedirectToActionResult("Index", "StageSelection", new { returnUrl = request.Path + request.QueryString });

                return;
            }
        }

        await next();
    }
}
