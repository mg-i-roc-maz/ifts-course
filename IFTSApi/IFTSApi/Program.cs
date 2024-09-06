using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);


// Aggiungi i servizi di autorizzazione
builder.Services.AddAuthorization();

// Aggiungi Swagger e configurazione per JWT Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "IFTS API", Version = "v1" });

    // Configura Swagger per utilizzare JWT Bearer Authentication
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Please enter the JWT token with the prefix 'Bearer '"
    });

    // Configura Swagger per richiedere il token JWT per tutte le richieste
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
            new List<string>()
        }
    });
});

// Aggiungi il contesto del database (se usi EF Core)
builder.Services.AddDbContext<IFTSDbContext>(options =>
    options.UseMySql(builder.Configuration.
    GetConnectionString("MySqlConnection"),
    ServerVersion.AutoDetect(builder.Configuration.
    GetConnectionString("MySqlConnection"))));

var app = builder.Build();

// Abilita Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFTS API V1");
});

// Configurazione della connessione a Redis
var redis = ConnectionMultiplexer.Connect("127.0.0.1:6379");
var db = redis.GetDatabase();


app.MapGet("/catalog", async (HttpContext context, IFTSDbContext dbContext) =>
{
    // Verifica se una richiesta per il catalogo VIP
    var isVip = Boolean.TryParse(context.Request.Query["vip"], out var vip) && vip;
    var keyForCatalog = isVip ? "catalog-vip" : "catalog";

    // Verifica se i dati sono già presenti in cache
    string cachedCatalog = await db.StringGetAsync(keyForCatalog);

    if (string.IsNullOrEmpty(cachedCatalog))
    {
        //inserisci un ritardo di 10 secondi per simulare un'operazione di I/O lenta
        await Task.Delay(10000);

        var allCatalog = await dbContext.Catalog.Where(i => i.Vip == isVip).ToListAsync();
        // Serializza nuovamente per salvarlo in Redis
        cachedCatalog = JsonSerializer.Serialize(allCatalog);

        // Salva i dati in cache per 2 minuti
        await db.StringSetAsync(keyForCatalog, cachedCatalog, TimeSpan.FromMinutes(2));
    }

    // Deserializza i dati dalla cache Redis
    var catalogo = JsonSerializer.Deserialize<List<Catalog>>(cachedCatalog);


    // Restituisce i dati del catalogo come risposta JSON
    return Results.Json(catalogo);
});

app.Run();

// Definizione della classe Elemento per rappresentare gli elementi nel JSON
public class Catalog
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("vip")]
    public bool Vip { get; set; }
}
