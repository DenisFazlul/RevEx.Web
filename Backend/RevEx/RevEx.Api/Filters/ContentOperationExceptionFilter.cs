using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RevEx.Application.Content;
namespace RevEx.Api.Filters;

public sealed class ContentOperationExceptionFilter(ProblemDetailsFactory problemDetails) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ContentOperationException error) return;
        context.Result = error.Failure switch
        {
            ContentFailure.NotFound => error.Detail is null ? new NotFoundResult() : new NotFoundObjectResult(new { message = error.Detail }),
            ContentFailure.BadRequest => new BadRequestObjectResult(new { message = error.Detail }),
            _ => new BadRequestObjectResult(problemDetails.CreateValidationProblemDetails(context.HttpContext, new ModelStateDictionary(), detail: error.Detail))
        };
        context.ExceptionHandled = true;
    }
}
