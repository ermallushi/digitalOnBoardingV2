using DigitalOnBoardingV2.Api.Services;

namespace DigitalOnBoardingV2.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit/{sessionId:guid}", GetAuditTrail).WithName("GetAuditTrail");
        return app;
    }

    private static IResult GetAuditTrail(Guid sessionId, IOnboardingSessionStore store, IAuditService audit)
    {
        var session = store.Get(sessionId);
        if (session is null) return Results.NotFound();
        return Results.Ok(audit.GetEntries(sessionId));
    }
}
