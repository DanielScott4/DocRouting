using Microsoft.AspNetCore.SignalR.Client;

namespace PadesSign.Web.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hub;
    public event Action<int>? StepSigned;
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(string baseUrl)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/signing")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<object>("StepSigned",    payload => StepSigned?.Invoke(0));
        _hub.On<object>("StatusChanged", payload => StatusChanged?.Invoke(payload.ToString()!));

        await _hub.StartAsync();
    }

    public Task JoinEnvelopeAsync(Guid envelopeId)
        => _hub?.InvokeAsync("JoinEnvelope", envelopeId.ToString()) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    { if (_hub != null) await _hub.DisposeAsync(); }
}