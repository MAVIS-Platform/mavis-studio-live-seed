var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ready", service = "payment" }));
app.MapGet("/pay", () => Results.Ok(new { status = "paid", service = "payment" }));
app.MapGet("/fail", () => Results.StatusCode(503));

app.Run();
