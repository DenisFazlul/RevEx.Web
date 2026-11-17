using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RevEx.Application.Content;
using RevEx.Application.Abstractions.Persistence;
namespace RevEx.Api.Filters;

public sealed class ContentOperationExceptionFilter(ProblemDetailsFactory problemDetails) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is PersistenceConflictException persistenceConflict)
        {
            SetProblem(context, 409, "persistence.conflict",
                "Данные конфликтуют с другой записью. Обновите данные и проверьте изменения.", persistenceConflict.Message);
            return;
        }

        if (context.Exception is not ContentOperationException error)
        {
            return;
        }

        var (status, code, fallback) = error.Failure switch
        {
            ContentFailure.NotFound => (404, "content.not_found", "Запись не найдена. Обновите список."),
            ContentFailure.Conflict => (409, "content.conflict", "Операция конфликтует с текущими данными. Обновите данные."),
            _ => (400, "content.invalid", "Проверьте введённые данные.")
        };
        SetProblem(context, status, code, error.Detail ?? fallback, error.Detail ?? fallback,
            error.Failure == ContentFailure.Validation);
    }

    private void SetProblem(ExceptionContext context, int status, string code, string detail, string message,
        bool isValidation = false)
    {
        var problem = isValidation
            ? problemDetails.CreateValidationProblemDetails(context.HttpContext, new ModelStateDictionary(), status, detail: detail)
            : problemDetails.CreateProblemDetails(context.HttpContext, status, detail: detail);
        problem.Extensions["kind"] = "controlled";
        problem.Extensions["code"] = code;
        problem.Extensions["message"] = message;
        context.Result = new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }
}
