using MedInsuranceHelper.Api.Configuration;
using MedInsuranceHelper.Api.Middleware;
using MedInsuranceHelper.Api.Services;
using MedInsuranceHelper.Api.Services.VectorStore;
using Serilog;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // --- Serilog ---
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration));

    // --- Configuration ---
    builder.Services.Configure<AppSettings>(
        builder.Configuration.GetSection(AppSettings.SectionName));

    // Override from environment variables (12-factor)
    builder.Services.PostConfigure<AppSettings>(s =>
    {
        s.FoundryApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY") ?? s.FoundryApiKey;
        s.FoundryEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT") ?? s.FoundryEndpoint;
        s.BlobConnectionString = Environment.GetEnvironmentVariable("BLOB_CONN_STRING") ?? s.BlobConnectionString;
        s.AppEnv = Environment.GetEnvironmentVariable("APP_ENV") ?? s.AppEnv;
    });

    // --- Core ASP.NET Core ---
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS — allow Angular dev server
    builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    // --- Application Services ---
    builder.Services.AddSingleton<IOfferRepository, InMemoryOfferRepository>();
    builder.Services.AddSingleton<ISessionService, SessionService>();
    builder.Services.AddSingleton<IFileVectorStore, FileVectorStore>();

    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
    builder.Services.AddScoped<IPdfIngestionService, PdfIngestionService>();
    builder.Services.AddScoped<IChunkingService, ChunkingService>();
    builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
    builder.Services.AddScoped<IFoundryClient, FoundryClient>();
    builder.Services.AddScoped<IRetrievalService, RetrievalService>();
    builder.Services.AddScoped<ILLMPipelineService, LLMPipelineService>();
    builder.Services.AddScoped<IComparisonService, ComparisonService>();
    builder.Services.AddScoped<IRecommendationService, RecommendationService>();

    var app = builder.Build();

    // --- Middleware pipeline ---
    app.UseGlobalErrorHandling();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapControllers();

    // Ensure blob container exists on startup (non-fatal)
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var blob = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
            await blob.EnsureContainerExistsAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not initialise blob container on startup (Azurite may not be running).");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

