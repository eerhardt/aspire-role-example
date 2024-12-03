using Azure;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddAzureBlobClient("blobs");

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/getcount", async (BlobServiceClient BlobService) =>
{
    var container = BlobService.GetBlobContainerClient("role-example");
//    await container.CreateIfNotExistsAsync();

    var blobClient = container.GetBlobClient("counter");

    var currentCount = 0;
    try
    {
        var contentResult = await blobClient.DownloadContentAsync();
        currentCount = contentResult.Value.Content.ToObjectFromJson<int>();
    }
    catch (RequestFailedException ex) when (ex.Status == 404)
    {
    }

    return currentCount;
});

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
