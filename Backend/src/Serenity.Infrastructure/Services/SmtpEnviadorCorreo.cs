using System.Net;
using System.Net.Mail;
using System.Text;
using Serenity.Application.Interfaces;

namespace Serenity.Infrastructure.Services;

/// <summary>Sección <c>Smtp</c>. Sin <c>Host</c> no se envía nada.</summary>
public sealed class OpcionesSmtp
{
    public string? Host { get; init; }
    public int Puerto { get; init; } = 587;
    public bool Ssl { get; init; } = true;
    public string? Usuario { get; init; }
    public string? Password { get; init; }
    public string Remitente { get; init; } = "";
    public string NombreRemitente { get; init; } = "Grupo Serenity";
}

/// <summary>Correo de texto plano por SMTP (STARTTLS con <c>Ssl=true</c>).</summary>
internal sealed class SmtpEnviadorCorreo(OpcionesSmtp opciones) : IEnviadorCorreo
{
    public bool Habilitado => !string.IsNullOrWhiteSpace(opciones.Host) && !string.IsNullOrWhiteSpace(opciones.Remitente);

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        using var mensaje = new MailMessage
        {
            From = new MailAddress(opciones.Remitente, opciones.NombreRemitente, Encoding.UTF8),
            Subject = m.Asunto,
            SubjectEncoding = Encoding.UTF8,
            Body = m.Texto,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        foreach (var para in m.Para)
        {
            mensaje.To.Add(para);
        }

        if (!string.IsNullOrWhiteSpace(m.ResponderA))
        {
            mensaje.ReplyToList.Add(m.ResponderA);
        }

        using var cliente = new SmtpClient(opciones.Host, opciones.Puerto)
        {
            EnableSsl = opciones.Ssl,
            Timeout = 15_000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(opciones.Usuario))
        {
            cliente.Credentials = new NetworkCredential(opciones.Usuario, opciones.Password);
        }

        await cliente.SendMailAsync(mensaje, ct);
    }
}
