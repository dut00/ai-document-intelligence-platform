using DocumentIntelligence.Application.Abstractions.Behaviors;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Documents.Processing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIntelligence.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Concrete handlers only: the generic decorators implement the same interfaces.
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(
                classes => classes.AssignableTo(typeof(ICommandHandler<,>)).Where(type => !type.IsGenericType),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(
                classes => classes.AssignableTo(typeof(IQueryHandler<,>)).Where(type => !type.IsGenericType),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Decorators wrap in registration order: logging ends up outermost, so it also records validation failures.
        services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IDateInsightsService, DateInsightsService>();

        return services;
    }
}
