using Microsoft.EntityFrameworkCore;
using ShoppingCartApi.Models;
using ShoppingCartApi.Data;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;


var builder = WebApplication.CreateBuilder(args);

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()       
              .AllowAnyMethod()       
              .AllowAnyHeader();      
    });
});


// Add services for EF Core and Swagger
builder.Services.AddDbContext<ShoppingCartContext>(opt => opt.UseInMemoryDatabase("ShoppingCartDb"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ShoppingCartApi", Version = "v1" });
});

var app = builder.Build();
// Seed the database with default products during startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ShoppingCartContext>();
    ShoppingCartContext.SeedData(dbContext); // Call the seed method
}

// Use the CORS policy
app.UseCors("AllowAll");

// Enable Swagger and Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// Redirect root "/" to "/swagger"
app.MapGet("/", () => Results.Redirect("/swagger"));

// === Product Endpoints (Grouped under 'Products') === //

// GET all products
app.MapGet("/products", async (ShoppingCartContext db) => await db.Products.ToListAsync())
    .WithTags("Products");

// GET specific product by ID
app.MapGet("/products/{id}", async (int id, ShoppingCartContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
}).WithTags("Products");

// POST add new product
app.MapPost("/products", async (Product product, ShoppingCartContext db) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
}).WithTags("Products");

// DELETE specific product by ID
app.MapDelete("/products/{id}", async (int id, ShoppingCartContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
    {
        return Results.NotFound();
    }

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Products");

#region insecure
// Insecure SQL Injection Endpoint
// app.MapGet("/products/insecure/{productName}", async (string productName, ShoppingCartContext db) =>
// {
//     // Insecure: Directly concatenate user input into an SQL query (SQL Injection)
//     var query = $"SELECT * FROM Products WHERE Name = '{productName}'";  // Vulnerable to SQL Injection
//     var products = await db.Products.FromSqlRaw(query).ToListAsync();

//     return products.Any() ? Results.Ok(products) : Results.NotFound();
// }).WithTags("Products");
#endregion

// === Cart Endpoints (Grouped under 'Cart') === //

// GET all cart items
app.MapGet("/cart", async (ShoppingCartContext db) => await db.CartItems.Include(c => c.Product).ToListAsync())
    .WithTags("Cart");

// GET specific cart item by ID
app.MapGet("/cart/{id}", async (int id, ShoppingCartContext db) =>
{
    var cartItem = await db.CartItems.Include(c => c.Product).FirstOrDefaultAsync(c => c.Id == id);
    return cartItem is not null ? Results.Ok(cartItem) : Results.NotFound();
}).WithTags("Cart");

// POST add item to cart
app.MapPost("/cart", async (CartItem cartItem, ShoppingCartContext db) =>
{
     // Find the product in the database using the ProductId
    var product = await db.Products.FindAsync(cartItem.ProductId);
    if (product == null)
    {
        return Results.NotFound("Product not found");
    }

    // Associate the tracked product with the cart item
    cartItem.Product = product;

    // Check if the cart item for this product already exists in the cart
    var existingCartItem = await db.CartItems
        .FirstOrDefaultAsync(c => c.ProductId == cartItem.ProductId);

    if (existingCartItem != null)
    {
        // If it exists, just update the quantity
        existingCartItem.Quantity += cartItem.Quantity;
    }
    else
    {
        // If it doesn't exist, add the new cart item
        db.CartItems.Add(cartItem);
    }

    // Save the changes
    await db.SaveChangesAsync();

    return Results.Created($"/cart/{cartItem.Id}", cartItem);
}).WithTags("Cart");

// DELETE specific cart item by ID
app.MapDelete("/cart/{id}", async (int id, ShoppingCartContext db) =>
{
    var cartItem = await db.CartItems.FindAsync(id);
    if (cartItem is null)
    {
        return Results.NotFound();
    }

    db.CartItems.Remove(cartItem);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Cart");

app.Run();