using Core.Contracts;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Email;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Api.Hubs;
using Infrastructure.Workers;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme (Example: 'Bearer {token}')",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    // Add XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Database
// Use SQL Server database
builder.Services.AddDbContext<MessagingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // SignalR JWT support and debugging
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/messageHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated successfully for user: {context.Principal?.FindFirst("sub")?.Value}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication failed: {context.Exception}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });



// SignalR
builder.Services.AddSignalR();

// Dependency Injection
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Background services
builder.Services.AddHostedService<EmailAlertWorker>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:5500",
                "http://localhost:5500",
                "http://localhost:5250",
                "http://34.242.41.55:5250"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Initialize database and seed demo data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();

    // Retry database connection with exponential backoff
    var maxRetries = 5;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            Console.WriteLine($"Database connection attempt {i + 1}/{maxRetries}...");
            await context.Database.CanConnectAsync();
            Console.WriteLine("✅ Database connection successful!");

            // Apply migrations
            await context.Database.MigrateAsync();
            Console.WriteLine("✅ Database migrations applied successfully!");

            // Seed data
            await SeedDemoData(context);
            Console.WriteLine("✅ Demo data seeded successfully!");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database connection failed: {ex.Message}");

            if (i == maxRetries - 1)
            {
                Console.WriteLine("💥 All database connection attempts failed. Exiting...");
                throw;
            }

            Console.WriteLine($"⏳ Retrying in {delay.TotalSeconds} seconds...");
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // Exponential backoff
        }
    }
}// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Messaging App API v1");
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.DefaultModelsExpandDepth(0);
    });
}

app.UseHttpsRedirection();
app.UseCors("ClientApp");
app.UseAuthentication();
app.UseAuthorization();


app.UseStaticFiles(); // Serve static files from wwwroot

// Add specific route for user files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/files"
});

// Default route to serve index.html for SPA
app.MapFallbackToFile("index.html");

app.MapControllers();
app.MapHub<MessageHub>("/messageHub");

// Serve dynamic config.js based on environment variables
app.MapGet("/config.js", (IConfiguration config, IWebHostEnvironment env) =>
{
    var apiBaseUrl = config["APP_API_BASE_URL"] ??
                     (env.IsDevelopment() ? "http://34.242.41.55:5250/api" : "http://34.242.41.55:5250/api");

    var configScript = $@"// Configuration loaded from server
window.APP_CONFIG = {{
    API_BASE_URL: '{apiBaseUrl}'
}};";

    return Results.Content(configScript, "application/javascript");
});

app.Run();

// Seed demo data for development
static async Task SeedDemoData(MessagingDbContext context)
{
    // Check if data already exists
    if (context.Users.Any()) return;

    var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();

    // Create demo users
    var users = new[]
    {
        new Core.Entities.User
        {
            Email = "alice@example.com",
            PasswordHash = passwordHasher.HashPassword(null!, "Demo@123"),
            CreatedAt = DateTime.UtcNow
        },
        new Core.Entities.User
        {
            Email = "bob@example.com",
            PasswordHash = passwordHasher.HashPassword(null!, "Demo@123"),
            CreatedAt = DateTime.UtcNow
        },
        new Core.Entities.User
        {
            Email = "charlie@example.com",
            PasswordHash = passwordHasher.HashPassword(null!, "Demo@123"),
            CreatedAt = DateTime.UtcNow
        },
        new Core.Entities.User
        {
            Email = "sistermagret@gmail.com",
            PasswordHash = passwordHasher.HashPassword(null!, "adminpassword"),
            CreatedAt = DateTime.UtcNow
        }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    // Create demo subscriptions
    var subscriptions = new[]
    {
        new Core.Entities.Subscription
        {
            UserId = 1,
            Feature = Core.Enums.FeatureType.GroupChat,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        },
        new Core.Entities.Subscription
        {
            UserId = 2,
            Feature = Core.Enums.FeatureType.FileSharing,
            StartDate = DateTime.UtcNow.AddDays(-15),
            EndDate = DateTime.UtcNow.AddDays(45)
        }
    };

    context.Subscriptions.AddRange(subscriptions);
    await context.SaveChangesAsync();

    // Create demo groups
    var groups = new[]
    {
        new Core.Entities.Group
        {
            Name = "Demo Team",
            Description = "A demo group for testing",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            Members = new List<Core.Entities.GroupMember>
            {
                new() { UserId = 1, Role = Core.Enums.GroupRole.Owner, JoinedAt = DateTime.UtcNow },
                new() { UserId = 2, Role = Core.Enums.GroupRole.Member, JoinedAt = DateTime.UtcNow }
            }
        }
    };

    context.Groups.AddRange(groups);
    await context.SaveChangesAsync();

    // Create demo messages
    var messages = new[]
    {
        new Core.Entities.Message
        {
            SenderId = 1,
            ReceiverId = 2,
            Content = "Hello Bob! Welcome to the messaging app!",
            SentAt = DateTime.UtcNow.AddMinutes(-30),
            IsRead = false
        },
        new Core.Entities.Message
        {
            SenderId = 2,
            ReceiverId = 1,
            Content = "Thanks Alice! This looks great!",
            SentAt = DateTime.UtcNow.AddMinutes(-25),
            IsRead = true
        },
        new Core.Entities.Message
        {
            SenderId = 1,
            GroupId = 1,
            Content = "Welcome to our demo team group!",
            SentAt = DateTime.UtcNow.AddMinutes(-20),
            IsRead = false
        }
    };

    context.Messages.AddRange(messages);
    await context.SaveChangesAsync();

    Console.WriteLine("Demo data seeded successfully!");
}

// Make Program class public for testing purposes
public partial class Program { }