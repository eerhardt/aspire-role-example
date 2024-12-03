using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Azure;
using RoleExample.Web;
using RoleExample.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

Console.WriteLine($"AZURE_CLIENT_ID2={Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")}");
builder.AddAzureBlobClient("blobs", settings =>
{
    //if (Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") is string clientId)
    //{
    //    var options = new ManagedIdentityCredentialOptions(ManagedIdentityId.FromUserAssignedClientId(clientId));
    //    options.Diagnostics.IsLoggingContentEnabled = true;
    //    options.Diagnostics.IsLoggingEnabled = true;
    //    options.Diagnostics.IsDistributedTracingEnabled = true;
    //    options.Diagnostics.LoggedHeaderNames.Add("x-ms-client-request-id");
    //    options.Diagnostics.LoggedHeaderNames.Add("x-ms-client-session-id");
    //    options.Diagnostics.LoggedQueryParameters.Add("api-version");
    //    options.Diagnostics.LoggedQueryParameters.Add("resource");
    //    options.Diagnostics.LoggedQueryParameters.Add("client_id");
    //    options.Diagnostics.IsAccountIdentifierLoggingEnabled = true;

    //    settings.Credential = new ManagedIdentityCredential(options);
    //}
    //var options = new DefaultAzureCredentialOptions();
    //options.Diagnostics.IsAccountIdentifierLoggingEnabled = true;
    //options.ManagedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");

    //settings.Credential = new DefaultAzureCredential(options);
}, clientBuilder =>
{
    clientBuilder.ConfigureOptions(options =>
    {
        options.Diagnostics.IsLoggingContentEnabled = true;
        options.Diagnostics.IsLoggingEnabled = true;
        options.Diagnostics.IsDistributedTracingEnabled = true;
        options.Diagnostics.LoggedHeaderNames.Add("Authorization");
        options.Diagnostics.LoggedHeaderNames.Add("x-ms-client-session-id");
        options.Diagnostics.LoggedQueryParameters.Add("api-version");
        options.Diagnostics.LoggedQueryParameters.Add("resource");
        options.Diagnostics.LoggedQueryParameters.Add("client_id");
    });
});

if (Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") is string clientId)
{
    var options = new ManagedIdentityCredentialOptions(ManagedIdentityId.FromUserAssignedClientId(clientId));
    options.Diagnostics.IsLoggingContentEnabled = true;
    options.Diagnostics.IsLoggingEnabled = true;
    options.Diagnostics.IsDistributedTracingEnabled = true;
    options.Diagnostics.LoggedHeaderNames.Add("x-ms-client-request-id");
    options.Diagnostics.LoggedHeaderNames.Add("x-ms-client-session-id");
    options.Diagnostics.LoggedQueryParameters.Add("api-version");
    options.Diagnostics.LoggedQueryParameters.Add("resource");
    options.Diagnostics.LoggedQueryParameters.Add("client_id");
    options.Diagnostics.IsAccountIdentifierLoggingEnabled = true;

    var cred = new ManagedIdentityCredential(options);

    var client = cred.GetType().GetProperty("Client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(cred);
    var result = (ValueTask<AccessToken>)client!.GetType().GetMethod("AuthenticateAsync")!.Invoke(client, [true, new TokenRequestContext(["https://storage.azure.com/.default"]), default(CancellationToken)])!;
    var token = await result;
    Console.WriteLine($"Token={token.Token}");

    // create an HTTP request and set the Bearer auth header
    var httpClient = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Get, Environment.GetEnvironmentVariable("ConnectionStrings__blobs") + "role-example/counter");
    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
    request.Headers.Add("x-ms-client-request-id", "ddf49918-342e-4d5e-8df2-28d1655ee291");
    request.Headers.Add("x-ms-return-client-request-id", "true");
    request.Headers.Add("User-Agent", "azsdk-net-Storage.Blobs/12.22.2 (.NET 9.0.0; Debian GNU/Linux 12 (bookworm))");
    try
    {
        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseContent}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex}");
    }
}


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
