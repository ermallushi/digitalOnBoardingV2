using DigitalOnBoardingV2.Api.Endpoints;
using DigitalOnBoardingV2.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Singleton stores (in-memory; replace with a database provider for production)
builder.Services.AddSingleton<IOnboardingSessionStore, InMemoryOnboardingSessionStore>();
builder.Services.AddSingleton<ISignatureStore, InMemorySignatureStore>();
builder.Services.AddSingleton<IAuditService, AuditService>();

// Scoped KYC services — swap implementations to plug in real vendor integrations
builder.Services.AddScoped<IDocumentVerificationService, DocumentVerificationService>();
builder.Services.AddScoped<IBiometricService, BiometricService>();
builder.Services.AddScoped<IRiskScoringService, RiskScoringService>();
builder.Services.AddScoped<IDigitalSignatureService, DigitalSignatureService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapOnboardingEndpoints();
app.MapInstoreEndpoints();
app.MapSignatureEndpoints();
app.MapAuditEndpoints();

app.Run();

public partial class Program;
