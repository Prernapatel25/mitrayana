using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Mitrayana.Api.Data;
using Mitrayana.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var builder = WebApplication.CreateBuilder(args);

// Load configuration
var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("DefaultConnection");

// Add Controllers + JSON camelCase (important for frontend compatibility)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for development frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDevFrontend", policy =>
    {
        policy.WithOrigins("http://127.0.0.1:8080", "http://localhost:8080", "http://127.0.0.1:5500", "http://localhost:5500")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// SQLite Database (fallback)
// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseSqlite("Data Source=mitrayana.db"));

// MySQL Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)
    )
);

// JWT Authentication
var jwtKey = configuration["Jwt:Key"];
var key = Encoding.UTF8.GetBytes(jwtKey!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["Jwt:Issuer"],
        ValidAudience = configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Authorization + Services
builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtService>();
// Register email service - always use SMTP for real deliveries; warn if configuration looks missing
builder.Services.AddScoped<Mitrayana.Api.Services.IEmailService, Mitrayana.Api.Services.SmtpEmailService>();
var smtpHost = builder.Configuration["Smtp:Host"] ?? string.Empty;
if (string.IsNullOrWhiteSpace(smtpHost) || smtpHost.Contains("example.com"))
{
    Console.WriteLine("[Warning] SMTP host appears unset or uses a placeholder (Smtp:Host). Emails will fail until configured with a real SMTP server.");
}
// CORS policy configured above as "AllowDevFrontend"

var app = builder.Build();

// Catch unhandled exceptions and ensure CORS headers are present on error responses
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.Headers["Access-Control-Allow-Origin"] = "http://127.0.0.1:8080";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type,Authorization";
        await context.Response.WriteAsync("Internal Server Error");
    });
});

// Global exception hooks to help debug unexpected shutdowns
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    Console.WriteLine("Unhandled exception: " + (e.ExceptionObject?.ToString() ?? "<null>"));
};
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Console.WriteLine("Unobserved task exception: " + e.Exception?.ToString());
};

// Log application lifetime events
var lifetime = app.Lifetime;
lifetime.ApplicationStopping.Register(() => Console.WriteLine("Application is stopping..."));
lifetime.ApplicationStopped.Register(() => Console.WriteLine("Application stopped."));

// Global middleware to catch exceptions and ensure CORS headers even on error
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Unhandled exception in middleware: " + ex);
        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = 500;
            context.Response.Headers["Access-Control-Allow-Origin"] = "http://localhost:5500";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,DELETE,OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type,Authorization";
            await context.Response.WriteAsync("Internal Server Error");
        }
        else
        {
            throw;
        }
    }
});

// Ensure CORS headers are present on every response (useful for dev & error responses)
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
        {
            context.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost:5500");
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type,Authorization");
        }
        return Task.CompletedTask;
    });

    if (context.Request.Method == Microsoft.AspNetCore.Http.HttpMethods.Options)
    {
        context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

// Enable Swagger in dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware pipeline - API routes FIRST, then static files
app.UseRouting();
// app.UseCors("AllowDevFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Log registered endpoints for debugging
try
{
    var dataSource = app.Services.GetService(typeof(Microsoft.AspNetCore.Routing.EndpointDataSource)) as Microsoft.AspNetCore.Routing.EndpointDataSource;
    if (dataSource != null)
    {
        Console.WriteLine("Registered endpoints:");
        foreach (var ep in dataSource.Endpoints)
        {
            Console.WriteLine(ep.DisplayName ?? ep.ToString());
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Endpoint listing failed: " + ex.Message);
}
// Serve static files (HTML, CSS, JS) AFTER API routes
app.UseStaticFiles();

// Note: removed generic fallback to avoid intercepting API POST requests
// If SPA fallback is required, implement a conditional fallback that ignores /api paths.

// --------------------------------------
// Database migration & admin seeding (with robust logging)
// --------------------------------------
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Apply migrations automatically (preferred over EnsureCreated when using Migrations)
            Console.WriteLine("Applying database migrations...");
            db.Database.Migrate();
            Console.WriteLine("Database migrations applied.");

            // Apply migrations automatically
            // Console.WriteLine("Applying database migrations...");
            // db.Database.Migrate();
            // Console.WriteLine("Database migrations applied.");
        }
        catch (Exception migEx)
        {
            Console.WriteLine("Database migration failed: " + migEx);
        }

        try
        {
            // Seed Admin user if none exists
            if (!db.Users.Any(u => u.Role == "Admin"))
            {
                var admin = new Mitrayana.Api.Models.User
                {
                    Name = "Admin",
                    Email = "admin@mitrayana.local",
                    ContactNumber = "0000000000",
                    Address = "",
                    Role = "Admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123")
                };

                db.Users.Add(admin);
                db.SaveChanges();
                Console.WriteLine("Admin user seeded.");
            }

            // Add a test user for forgot password
            if (!db.Users.Any(u => u.Email == "test@example.com"))
            {
                var testUser = new Mitrayana.Api.Models.User
                {
                    Name = "Test User",
                    Email = "test@example.com",
                    ContactNumber = "1234567890",
                    Address = "Test Address",
                    Role = "Senior",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123")
                };

                db.Users.Add(testUser);
                db.SaveChanges();
                Console.WriteLine("Test user seeded.");
            }

            // Seed sample users if none exist
            // if (db.Users.Count() == 1) // Only admin
            // {
            //     var sampleUsers = new[]
            //     {
            //         new Mitrayana.Api.Models.User { Name = "John Doe", Email = "john@example.com", ContactNumber = "1234567890", Address = "123 Main St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Jane Smith", Email = "jane@example.com", ContactNumber = "0987654321", Address = "456 Elm St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Bob Johnson", Email = "bob@example.com", ContactNumber = "1112223333", Address = "789 Oak St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Alice Brown", Email = "alice@example.com", ContactNumber = "4445556666", Address = "101 Pine St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Charlie Wilson", Email = "charlie@example.com", ContactNumber = "7778889999", Address = "202 Maple St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Diana Davis", Email = "diana@example.com", ContactNumber = "0001112222", Address = "303 Birch St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Edward Miller", Email = "edward@example.com", ContactNumber = "3334445555", Address = "404 Cedar St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Fiona Garcia", Email = "fiona@example.com", ContactNumber = "6667778888", Address = "505 Walnut St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "George Martinez", Email = "george@example.com", ContactNumber = "9990001111", Address = "606 Chestnut St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Helen Rodriguez", Email = "helen@example.com", ContactNumber = "2223334444", Address = "707 Spruce St", Role = "Senior", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") },
            //         new Mitrayana.Api.Models.User { Name = "Ian Lopez", Email = "ian@example.com", ContactNumber = "5556667777", Address = "808 Fir St", Role = "ServiceProvider", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123") }
            //     };

            //     db.Users.AddRange(sampleUsers);
            //     db.SaveChanges();
            //     Console.WriteLine("Sample users seeded.");
            // }

            // Seed sample service requests if none exist
            if (!db.ServiceRequests.Any())
            {
                var seniors = db.Users.Where(u => u.Role == "Senior").ToList();
                var serviceProvider = db.Users.FirstOrDefault(u => u.Role == "ServiceProvider");

                var sampleRequests = new List<Mitrayana.Api.Models.ServiceRequest>();
                foreach (var senior in seniors.Take(5)) // First 5 seniors
                {
                    sampleRequests.Add(new Mitrayana.Api.Models.ServiceRequest
                    {
                        UserId = senior.UserId,
                        Title = "Health Assistance",
                        SubService = "Doctor Visit",
                        Description = "Need help with medical checkup",
                        Contact = senior.ContactNumber,
                        Status = "Open",
                        AssignedVolunteerId = serviceProvider?.UserId,
                        AssignedVolunteerName = serviceProvider?.Name,
                        PinCode = "123456"
                    });
                }

                db.ServiceRequests.AddRange(sampleRequests);
                db.SaveChanges();
                Console.WriteLine("Sample service requests seeded.");
            }

            // Seed sample feedbacks if none exist
            if (!db.Feedbacks.Any())
            {
                var users = db.Users.Where(u => u.Role == "Senior" || u.Role == "ServiceProvider").Take(5).ToList();
                var sampleFeedbacks = new List<Mitrayana.Api.Models.Feedback>();
                foreach (var user in users)
                {
                    sampleFeedbacks.Add(new Mitrayana.Api.Models.Feedback
                    {
                        UserId = user.UserId,
                        Message = $"Great service from {user.Name}! Highly recommend.",
                        Rating = 5,
                        DateCreated = DateTime.UtcNow.AddDays(-1)
                    });
                }

                db.Feedbacks.AddRange(sampleFeedbacks);
                db.SaveChanges();
                Console.WriteLine("Sample feedbacks seeded.");
            }

            // Seed sample services if none exist
            if (!db.Services.Any())
            {
                var sampleServices = new List<Mitrayana.Api.Models.Service>
                {
                    new Mitrayana.Api.Models.Service { Name = "Home Cleaning", Category = "Household", Description = "Professional home cleaning services", Price = 500 },
                    new Mitrayana.Api.Models.Service { Name = "Cooking Assistance", Category = "Household", Description = "Help with cooking meals", Price = 300 },
                    new Mitrayana.Api.Models.Service { Name = "Medical Checkup", Category = "Health", Description = "Basic health checkup at home", Price = 1000 },
                    new Mitrayana.Api.Models.Service { Name = "Grocery Shopping", Category = "Errands", Description = "Help with grocery shopping", Price = 200 },
                    new Mitrayana.Api.Models.Service { Name = "Companionship", Category = "Social", Description = "Friendly companionship visits", Price = 400 }
                };

                db.Services.AddRange(sampleServices);
                db.SaveChanges();
                Console.WriteLine("Sample services seeded.");
            }

            // Seed sample subservices if none exist
            if (!db.SubServices.Any())
            {
                var services = db.Services.ToList();
                var sampleSubServices = new List<Mitrayana.Api.Models.SubService>();
                if (services.Any())
                {
                    var homeCleaning = services.FirstOrDefault(s => s.Name == "Home Cleaning");
                    if (homeCleaning != null)
                    {
                        sampleSubServices.Add(new Mitrayana.Api.Models.SubService { ServiceId = homeCleaning.ServiceId, Name = "Deep Cleaning", Price = 600 });
                        sampleSubServices.Add(new Mitrayana.Api.Models.SubService { ServiceId = homeCleaning.ServiceId, Name = "Regular Cleaning", Price = 400 });
                    }
                    var cooking = services.FirstOrDefault(s => s.Name == "Cooking Assistance");
                    if (cooking != null)
                    {
                        sampleSubServices.Add(new Mitrayana.Api.Models.SubService { ServiceId = cooking.ServiceId, Name = "Meal Preparation", Price = 350 });
                    }
                }

                db.SubServices.AddRange(sampleSubServices);
                db.SaveChanges();
                Console.WriteLine("Sample subservices seeded.");
            }
        }
        catch (Exception seedEx)
        {
            Console.WriteLine("Seeding failed: " + seedEx);
        }

        // ----------------------
        // Migrate existing Volunteer -> ServiceProvider
        // ----------------------
        try
        {
            var volunteers = db.Users.Where(u => (u.Role ?? "").ToLower() == "volunteer").ToList();
            if (volunteers.Any())
            {
                foreach (var v in volunteers)
                {
                    v.Role = "ServiceProvider";
                }
                db.SaveChanges();
                Console.WriteLine($"Migrated {volunteers.Count} user(s) from 'Volunteer' to 'ServiceProvider'.");
            }

            // Previously the application reset verified service providers at startup.
            // That behavior was removed so that admin approvals persist across restarts.
        }
        catch (Exception ex)
        {
            Console.WriteLine("Role migration failed: " + ex);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Database migration/seed block failed: " + ex);
}

// Log a summary of the Users table at startup (helpful for debugging)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Mitrayana.Api.Data.ApplicationDbContext>();
        var userCount = db.Users.Count();
        Console.WriteLine($"User table rows: {userCount}");

        // Print up to 20 sample users (id, email, name, role)
        var samples = db.Users.OrderBy(u => u.UserId).Take(20).Select(u => new { u.UserId, u.Email, u.Name, u.Role, u.IsActive, u.CreatedAt }).ToList();
        if (samples.Any())
        {
            Console.WriteLine("Sample users:");
            foreach (var s in samples)
            {
                Console.WriteLine($"- [{s.UserId}] {s.Email} | {s.Name} | Role: {s.Role} | Active: {s.IsActive} | Created: {s.CreatedAt:o}");
            }
        }
        else
        {
            Console.WriteLine("No users found in the Users table.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Failed to list users at startup: " + ex);
}

app.MapGet("/", context =>
{
    context.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();

