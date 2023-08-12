using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme() {
        Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
builder.Services.AddDbContext<CustomerDbContext>();
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization(options => {
    options.AddPolicy("DeleteUser", policy => policy.RequireClaim("can_delete_user", "true"));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

var logger = LoggerFactory.Create(config =>
    {
        config.AddConsole();
    }).CreateLogger("CustomerApi");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapGet("/customers", [Authorize]async (CustomerDbContext context) =>
{
    logger.LogInformation("Getting customers...");
    var customers = await context.Customer.ToListAsync();
    logger.LogInformation("Retrieved {Count} customers", customers.Count);
    return Results.Ok(customers);
});

app.MapGet("/customers/{id}", async (string id, CustomerDbContext context) =>
{
    var customer = await context.Customer.FindAsync(id);
    if (customer == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(customer);
}).RequireAuthorization();

app.MapPost("/customers", async (Customer customer, CustomerDbContext context) =>
{
    context.Customer.Add(customer);
    await context.SaveChangesAsync();
    return Results.Created($"/customers/{customer.Id}", customer);
}).RequireAuthorization(new AuthorizeAttribute() { Roles = "Admin" });

app.MapPut("/customers/{id}", async (string id, Customer customer, CustomerDbContext context) =>
{
    var currentCustomer = await context.Customer.FindAsync(id);
    if (currentCustomer == null)
    {
        return Results.NotFound();
    }

    context.Entry(currentCustomer).CurrentValues.SetValues(customer);
    await context.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/customers/{id}", async (string id, CustomerDbContext context) =>
{
    var currentCustomer = await context.Customer.FindAsync(id);

    if (currentCustomer == null)
    {
        return Results.NotFound();
    }

    context.Customer.Remove(currentCustomer);
    await context.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization(new AuthorizeAttribute() { Policy = "DeleteUser" });

app.Run();
