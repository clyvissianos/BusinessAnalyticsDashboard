using BusinessAnalytics.Api;
using BusinessAnalytics.Application.Analytics;
using BusinessAnalytics.Infrastructure.Analytics;
using BusinessAnalytics.Infrastructure.Extensions;
using BusinessAnalytics.Infrastructure.Parsing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;


// ✅ Register ExcelDataReader encodings ONCE before anything else
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// Infrastructure (EF + Identity + AutoMapper)
builder.Services.AddInfrastructure(builder.Configuration);

// Auth/JWT
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;     // ok for local dev
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
        // (Optional) log why validation fails:
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"JWT fail: {ctx.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Controllers & Swagger
builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(ApiAssemblyMarker).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<ISalesAnalyticsService, SalesAnalyticsService>();
builder.Services.AddScoped<ISalesParser, SalesParser>();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BusinessAnalytics API", Version = "v1" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // 1) Define the scheme with id "Bearer"
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // 2) IMPORTANT: reference the scheme by Id in the requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // 🔧 Fix: if two types share the same short name, use FullName in schema ids
    //c.CustomSchemaIds(t => t.FullName);


    // 🧭 Map modern date/time types (prevents 500 during JSON schema generation)
    //c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    //c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });

    // Optional (helps with nullable refs in schemas)
    //c.SupportNonNullableReferenceTypes();
});

var app = builder.Build();

// Migrate & seed only outside integration tests
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.EnsureDatabaseSeededAsync();
}

// Pipeline
if (app.Environment.IsDevelopment())
{
  
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "BusinessAnalytics API v1");
        o.RoutePrefix = "";

        o.DocumentTitle = "BusinessAnalytics API";

        o.HeadContent = @"
    <style>
    .notice-box {
        padding: 12px;
        margin-bottom: 12px;
        border-left: 5px solid #007acc;
        background: #f0f7ff;
        font-family: Segoe UI, sans-serif;
    }
    </style>
    <div class='notice-box'>
    <b>Tip:</b> Prefer the <b>XLSX template</b> for uploading Sales data.  
    CSV files are UTF-8 encoded and may appear unreadable if opened directly in Excel.  
    Use <i>Data → From Text/CSV → UTF-8</i> to view CSV files correctly.
    </div>";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the implicit Program class public so integration tests can access it
public partial class Program { }
