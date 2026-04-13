using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using taskFlow.Exceptions;

namespace taskFlow.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var title = "Internal server error";
            var errorType = exception.GetType().Name;
            var message = "An unexpected error occurred. Please try again later.";

            switch (exception)
            {
                case ValidationException validationException:
                    statusCode = HttpStatusCode.BadRequest;
                    title = "Validation failed";
                    message = validationException.Message;
                    break;
                case KeyNotFoundException notFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    title = "Resource not found";
                    message = notFoundException.Message;
                    break;
                case ForbiddenException forbiddenException:
                    statusCode = HttpStatusCode.Forbidden;
                    title = "Forbidden";
                    message = forbiddenException.Message;
                    break;
                case UnauthorizedAccessException unauthorizedException:
                    statusCode = HttpStatusCode.Unauthorized;
                    title = "Unauthorized";
                    message = unauthorizedException.Message;
                    break;
            }

            _logger.LogError(exception, "Unhandled exception while processing request {Method} {Path}: {ErrorType}",
                context.Request.Method,
                context.Request.Path,
                errorType);

            var errorResponse = new ErrorResponse(
                Status: false,
                Message: message,
                Error: new ErrorDetail(errorType, title, message),
                TraceId: context.TraceIdentifier);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
        }

        private sealed record ErrorDetail(string Type, string Title, string Detail);
        private sealed record ErrorResponse(bool Status, string Message, ErrorDetail Error, string TraceId);
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
