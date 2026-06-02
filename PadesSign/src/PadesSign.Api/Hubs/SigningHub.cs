using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PadesSign.Api.Hubs;

[Authorize]
public class SigningHub : Hub
{
    /// <summary>Client calls this to join the real-time group for an envelope.</summary>
    public async Task JoinEnvelope(string envelopeId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"envelope:{envelopeId}");

    public async Task LeaveEnvelope(string envelopeId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"envelope:{envelopeId}");
}