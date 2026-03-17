using OktaInlineHookPermitIOIntegration.Helpers;
using OktaInlineHookPermitIOIntegration.Repository;
using OktaInlineHookPermitIOIntegration.Services;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IConfigHelper, ConfigHelper>();
builder.Services.AddScoped<IOktaUserRepository, OktaUserRepository>();

builder.Services.AddHttpClient<IPermitApiClient, PermitApiClient>(client =>
{
    var baseUrl = builder.Configuration["Permit:BaseUrl"] ?? "https://api.permit.io/";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Permit:ApiKey"]);
});

builder.Services.AddControllers()
    .AddNewtonsoftJson();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
