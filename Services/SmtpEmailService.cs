using System.Net;
using System.Net.Mail;

namespace MaintenanceSandbox.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _from;

    public SmtpEmailService(string host, int port, string user, string password, string from)
    {
        _host     = host;
        _port     = port;
        _user     = user;
        _password = password;
        _from     = from;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl   = true,
            Credentials = new NetworkCredential(_user, _password)
        };

        using var msg = new MailMessage(_from, toEmail, subject, body)
        {
            IsBodyHtml = false
        };

        await client.SendMailAsync(msg);
    }
}
