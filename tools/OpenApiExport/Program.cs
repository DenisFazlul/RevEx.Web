using System.Globalization;
using Microsoft.OpenApi.Writers;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using Swashbuckle.AspNetCore.Swagger;

// Build only the MVC/Swagger service graph: no server, databases, storage or Keycloak needed.
var builder = WebApplication.CreateBuilder();
builder.Configuration["Authentication:Provider"] = "Keycloak";
builder.Services.AddControllers().AddApplicationPart(typeof(ContentController).Assembly);
builder.Services.AddApiSwagger(builder.Configuration);
await using var app = builder.Build();
var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
var output = args.Length == 1 ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/openapi.json"));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
using var text = new StringWriter(CultureInfo.InvariantCulture);
document.SerializeAsV3(new OpenApiJsonWriter(text));
await File.WriteAllTextAsync(output, text.ToString() + Environment.NewLine);
Console.WriteLine($"OpenAPI: {output} ({document.Paths.Count} paths)");
