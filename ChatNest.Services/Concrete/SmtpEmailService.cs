using ChatNest.Services.Abstract;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using System.Net;
using System.Net.Sockets;

namespace ChatNest.Services.Concrete
{
    public sealed class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _configuration["Email:Smtp:Host"];
            var port = int.TryParse(_configuration["Email:Smtp:Port"], out var parsedPort) ? parsedPort : 587;
            var username = _configuration["Email:Smtp:Username"];
            var password = _configuration["Email:Smtp:Password"];
            var fromEmail = _configuration["Email:Smtp:FromEmail"] ?? username;
            var fromName = _configuration["Email:Smtp:FromName"] ?? "ChatNest";
            var enableSsl = bool.TryParse(_configuration["Email:Smtp:EnableSsl"], out var sslValue)
                ? sslValue
                : port is 465 or 587;

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError(
                    "SMTP config invalid. HostConfigured={HostConfigured}, UsernameConfigured={UsernameConfigured}, PasswordConfigured={PasswordConfigured}, FromEmailConfigured={FromEmailConfigured}",
                    !string.IsNullOrWhiteSpace(host),
                    !string.IsNullOrWhiteSpace(username),
                    !string.IsNullOrWhiteSpace(password),
                    !string.IsNullOrWhiteSpace(fromEmail));
                throw new InvalidOperationException("SMTP email settings are not configured correctly.");
            }

            _logger.LogInformation(
                "SMTP send requested. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, Username={Username}, From={FromEmail}, To={ToEmail}, Subject={Subject}, BodyLength={BodyLength}",
                host,
                port,
                enableSsl,
                username,
                fromEmail,
                toEmail,
                subject,
                htmlBody?.Length ?? 0);

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                _logger.LogInformation(
                    "SMTP host resolved. Host={Host}, Addresses={Addresses}",
                    host,
                    string.Join(", ", addresses.Select(a => a.ToString())));
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "SMTP host DNS resolution failed. Host={Host}", host);
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart(TextFormat.Html)
            {
                Text = htmlBody
            };

            var secureSocketOptions = ResolveSecureSocketOptions(port, enableSsl);

            using var smtpClient = new SmtpClient
            {
                Timeout = 30000
            };

            try
            {
                _logger.LogInformation(
                    "SMTP client connecting. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, SecureSocketOptions={SecureSocketOptions}",
                    host,
                    port,
                    enableSsl,
                    secureSocketOptions);

                await smtpClient.ConnectAsync(host, port, secureSocketOptions);

                _logger.LogInformation(
                    "SMTP connected. IsSecure={IsSecure}, IsAuthenticated={IsAuthenticated}, Capabilities={Capabilities}, AuthenticationMechanisms={AuthenticationMechanisms}",
                    smtpClient.IsSecure,
                    smtpClient.IsAuthenticated,
                    smtpClient.Capabilities,
                    string.Join(", ", smtpClient.AuthenticationMechanisms));

                _logger.LogInformation("SMTP authenticating. Username={Username}", username);
                await smtpClient.AuthenticateAsync(username, password);

                _logger.LogInformation(
                    "SMTP authenticated successfully. IsAuthenticated={IsAuthenticated}, To={ToEmail}",
                    smtpClient.IsAuthenticated,
                    toEmail);

                _logger.LogInformation("SMTP sending message. To={ToEmail}, Subject={Subject}", toEmail, subject);
                await smtpClient.SendAsync(message);
                _logger.LogInformation("Password reset email sent successfully to {Email}", toEmail);

                await smtpClient.DisconnectAsync(true);
                _logger.LogInformation("SMTP disconnected cleanly. Host={Host}", host);
            }
            catch (MailKit.Net.Smtp.SmtpCommandException ex)
            {
                _logger.LogError(
                    ex,
                    "SMTP command failed. ErrorCode={ErrorCode}, StatusCode={StatusCode}, Host={Host}, Port={Port}, EnableSsl={EnableSsl}, SecureSocketOptions={SecureSocketOptions}, Username={Username}, From={FromEmail}, To={ToEmail}, InnerError={InnerError}",
                    ex.ErrorCode,
                    ex.StatusCode,
                    host,
                    port,
                    enableSsl,
                    secureSocketOptions,
                    username,
                    fromEmail,
                    toEmail,
                    ex.InnerException?.Message);
                throw;
            }
            catch (MailKit.Net.Smtp.SmtpProtocolException ex)
            {
                _logger.LogError(
                    ex,
                    "SMTP protocol failed. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, SecureSocketOptions={SecureSocketOptions}, Username={Username}, To={ToEmail}, InnerError={InnerError}",
                    host,
                    port,
                    enableSsl,
                    secureSocketOptions,
                    username,
                    toEmail,
                    ex.InnerException?.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected email send failure. Host={Host}, Port={Port}, EnableSsl={EnableSsl}, SecureSocketOptions={SecureSocketOptions}, To={ToEmail}",
                    host,
                    port,
                    enableSsl,
                    secureSocketOptions,
                    toEmail);
                throw;
            }
        }

        private static SecureSocketOptions ResolveSecureSocketOptions(int port, bool enableSsl)
        {
            if (!enableSsl)
            {
                return SecureSocketOptions.None;
            }

            return port switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                25 => SecureSocketOptions.StartTlsWhenAvailable,
                _ => SecureSocketOptions.StartTls
            };
        }
    }
}
