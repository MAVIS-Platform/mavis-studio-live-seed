var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

string PaymentUrl() =>
    (Environment.GetEnvironmentVariable("PAYMENT_URL") ?? "http://orders-payment-route:8080").TrimEnd('/');

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "orders" }));

app.MapGet("/orders", async (IHttpClientFactory clients) =>
{
    try
    {
        var payment = await clients.CreateClient().GetStringAsync($"{PaymentUrl()}/pay");
        return Results.Ok(new { id = "order-001", payment });
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(502);
    }
});

app.MapGet("/failed-order", async (IHttpClientFactory clients) =>
{
    try
    {
        using var response = await clients.CreateClient().GetAsync($"{PaymentUrl()}/fail");
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(502);
    }
});

app.Run();
