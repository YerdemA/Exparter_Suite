using System.Security.Claims;
using Exparter.Api.Common;
using Exparter.Application.Messaging;
using Mediator;

namespace Exparter.Api.Messaging;

internal sealed record SendMessageRequest(string? Body, Guid? ClientMessageId);

internal static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder routes)
    {
        var conversations = routes.MapGroup("/conversations").RequireAuthorization();

        conversations.MapGet("", async (int? page, int? pageSize, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListConversationsQuery(user.GetUserId(), page, pageSize), cancellationToken)));

        conversations.MapGet("/{conversationId}", async (Guid conversationId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetConversationQuery(user.GetUserId(), conversationId), cancellationToken)));

        conversations.MapGet("/{conversationId}/messages", async (Guid conversationId, string? cursor, int? limit, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListMessagesQuery(user.GetUserId(), conversationId, cursor, limit), cancellationToken)));

        conversations.MapPost("/{conversationId}/messages", async (Guid conversationId, SendMessageRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var message = await sender.Send(
                new SendMessageCommand(user.GetUserId(), conversationId, request.Body ?? string.Empty, request.ClientMessageId),
                cancellationToken);
            return Results.Created($"/api/v1/conversations/{conversationId}/messages", message);
        });

        conversations.MapPost("/{conversationId}/read", async (Guid conversationId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new MarkConversationReadCommand(user.GetUserId(), conversationId), cancellationToken);
            return Results.NoContent();
        });

        conversations.MapPost("/{conversationId}/close", async (Guid conversationId, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CloseConversationCommand(user.GetUserId(), conversationId), cancellationToken)));

        return routes;
    }
}
