using System.Collections.Concurrent;

namespace AtlasTaskFlow.Security;

public record OutgoingEmail(DateTime SentAt, string To, string Subject, string Body, string? Link);

/// <summary>Sends the account emails: email confirmation and password reset.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, string? link);
}

/// <summary>
/// Atlas has no email service yet (DR-0013), so "sending" an email writes it to the app log. On a developer PC the most
/// recent ones are also kept in memory for the Development-only mailbox page (/dev/mailbox), which makes the
/// confirmation and reset flows easy to try and screenshot. Nothing here leaves the server.
/// </summary>
public class LogEmailSender : IEmailSender
{
    private const int Keep = 25;
    private readonly ConcurrentQueue<OutgoingEmail> _recent = new();
    private readonly ILogger<LogEmailSender> _logger;
    private readonly bool _keepForMailbox;

    public LogEmailSender(ILogger<LogEmailSender> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _keepForMailbox = env.IsDevelopment();
    }

    public Task SendAsync(string to, string subject, string body, string? link)
    {
        if (_keepForMailbox)
        {
            // Links carry one-time tokens, so they are only logged and kept on a developer PC, never on the live site.
            _logger.LogInformation("Email to {To}: {Subject}. Link: {Link}", to, subject, link);
            _recent.Enqueue(new OutgoingEmail(DateTime.Now, to, subject, body, link));
            while (_recent.Count > Keep) _recent.TryDequeue(out _);
        }
        else
        {
            _logger.LogInformation("Email \"{Subject}\" would be sent here once Atlas has an email service.", subject);
        }
        return Task.CompletedTask;
    }

    /// <summary>Newest first. Always empty outside Development.</summary>
    public IReadOnlyList<OutgoingEmail> Recent => _recent.Reverse().ToList();
}
