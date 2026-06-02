using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace PadesSign.Infrastructure.Notifications;

public class EmailNotificationService : INotificationService
{
    private readonly SendGridClient _sg;
    private readonly string _fromEmail;
    private readonly string _appBaseUrl;

    public EmailNotificationService(string apiKey, string fromEmail, string appBaseUrl)
    { _sg = new SendGridClient(apiKey); _fromEmail = fromEmail; _appBaseUrl = appBaseUrl; }

    public async Task NotifySignatoryAsync(DocumentEnvelope env, WorkflowStep step, CancellationToken ct)
    {
        // In production, look up the user's email by step.AssigneeId from IUserRepository
        var signUrl = $"{_appBaseUrl}/sign/{env.Id}";
        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress(_fromEmail, "PadesSign"),
            new EmailAddress("signatory@example.com"),  // TODO: resolve from user store
            $"Action required: sign \"{env.OriginalFileName}\"",
            $"Please sign the document at: {signUrl}",
            $"<p>Please <a href=\"{signUrl}\">sign the document</a>.</p>");
        await _sg.SendEmailAsync(msg, ct);
    }

    public async Task NotifyCompletedAsync(DocumentEnvelope env, CancellationToken ct)
    {
        var downloadUrl = $"{_appBaseUrl}/documents/{env.Id}";
        var msg = MailHelper.CreateSingleEmail(
            new EmailAddress(_fromEmail, "PadesSign"),
            new EmailAddress("owner@example.com"),
            $"Signing complete: \"{env.OriginalFileName}\"",
            $"All parties have signed. Download at: {downloadUrl}",
            $"<p>All parties have signed. <a href=\"{downloadUrl}\">Download</a>.</p>");
        await _sg.SendEmailAsync(msg, ct);
    }

    public Task NotifyDeclinedAsync(DocumentEnvelope env, Guid userId, CancellationToken ct)
        => Task.CompletedTask; // TODO: implement decline notification
}