var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("upstream", client => client.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "orders-payment-route" }));

app.MapMethods("/{**path}", ["GET", "HEAD"], async (HttpContext context, IHttpClientFactory clients) =>
{
    var upstream = Environment.GetEnvironmentVariable("UPSTREAM_URL");
    if (!Uri.TryCreate(upstream, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "http")
        return Results.StatusCode(503);

    var destination = new Uri(endpoint, context.Request.Path + context.Request.QueryString);
    try
    {
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), destination);
        using var response = await clients.CreateClient("upstream").SendAsync(request, context.RequestAborted);
        var content = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        await context.Response.Body.WriteAsync(content, context.RequestAborted);
        return Results.Empty;
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(502);
    }
    catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
    {
        return Results.StatusCode(504);
    }
});

app.Run();
