using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PadesSign.Application.Interfaces;
using PadesSign.Application.Workflows;
using PadesSign.Infrastructure.Data;
using PadesSign.Infrastructure.Data.Repositories;
using PadesSign.Infrastructure.Notifications;
using PadesSign.Infrastructure.Pdf;
using PadesSign.Infrastructure.Storage;

namespace PadesSign.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<PadesSignDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEnvelopeRepository, EnvelopeRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<WorkflowOrchestrator>();

        services.AddSingleton<IBlobStorage>(_ =>
            new AzureBlobStorage(
                config["Azure:Storage:ConnectionString"]!,
                config["Azure:Storage:Container"]!));

        services.AddSingleton<ISigningSessionStore, InMemorySigningSessionStore>();

        services.AddScoped<IPadesSigningService>(sp =>
            new PadesSigningService(
                sp.GetRequiredService<IBlobStorage>(),
                new PadesOptions
                {
                    TsaUrl      = config["Pades:TsaUrl"]!,
                    TsaLogin    = config["Pades:TsaLogin"] ?? string.Empty,
                    TsaPassword = config["Pades:TsaPassword"] ?? string.Empty
                }));

        services.AddScoped<INotificationService>(sp =>
            new EmailNotificationService(
                config["SendGrid:ApiKey"]!,
                config["SendGrid:FromEmail"]!,
                config["App:BaseUrl"]!));

        return services;
    }
}