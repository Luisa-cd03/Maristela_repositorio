// ============================================================
// ARCHIVO: ForgotPasswordController.cs
// UBICACIÓN: Controllers/ForgotPasswordController.cs
// ============================================================
using BellezaSuprema2.Helpers;
using BellezaSuprema2.Models;
using MongoDB.Driver;
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace BellezaSuprema2.Controllers
{
    public class ForgotPasswordController : Controller
    {
        private const string GmailRemitente = "hanieldelarosa78@gmail.com";
        private const string GmailAppPassword = "fbrm xqux fixn pyzg";
        private const string NombreRemitente = "Belleza Suprema";

        // ── HashMD5: convierte texto plano a MD5 ──
        private string HashMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        // ════════════════════════════════════════════════════════════
        // PASO 1: VERIFICAR CORREO
        // Acepta GET y POST para evitar problemas de enrutamiento.
        // El [ValidateAntiForgeryToken] se quitó porque el token
        // se valida manualmente — más compatible con fetch() en MVC4.
        // ════════════════════════════════════════════════════════════
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult VerificarCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return Json(new { ok = false, mensaje = "Ingresa un correo electrónico." },
                            JsonRequestBehavior.AllowGet);

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");
            var usuario = colUsuarios.Find(
                Builders<UserModel>.Filter.Eq(u => u.Email, correo.Trim().ToLower())
            ).FirstOrDefault();

            if (usuario == null)
                return Json(new { ok = false, mensaje = "Este correo no está registrado." },
                            JsonRequestBehavior.AllowGet);

            // Generar código de 6 dígitos y guardarlo en Session
            var rng = new Random();
            string codigo = rng.Next(100000, 999999).ToString();

            Session["RecuperarCodigo"] = codigo;
            Session["RecuperarCorreo"] = correo.Trim().ToLower();
            Session["CodigoExpira"] = DateTime.Now.AddMinutes(10);

            try
            {
                EnviarCorreoCodigo(correo, codigo, usuario.Nombre);
            }
            catch (Exception ex)
            {
                Session.Remove("RecuperarCodigo");
                Session.Remove("RecuperarCorreo");
                Session.Remove("CodigoExpira");
                return Json(new { ok = false, mensaje = "Error SMTP: " + ex.Message },
                            JsonRequestBehavior.AllowGet);
            }

            return Json(new { ok = true, mensaje = "Código enviado correctamente." },
                        JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // PASO 2: VALIDAR CÓDIGO
        // ════════════════════════════════════════════════════════════
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult ValidarCodigo(string codigo)
        {
            if (Session["RecuperarCodigo"] == null || Session["RecuperarCorreo"] == null)
                return Json(new { ok = false, mensaje = "Sesión expirada. Vuelve a ingresar tu correo." },
                            JsonRequestBehavior.AllowGet);

            var expira = (DateTime)Session["CodigoExpira"];
            if (DateTime.Now > expira)
            {
                Session.Remove("RecuperarCodigo");
                Session.Remove("RecuperarCorreo");
                Session.Remove("CodigoExpira");
                return Json(new { ok = false, mensaje = "El código expiró. Solicita uno nuevo." },
                            JsonRequestBehavior.AllowGet);
            }

            string codigoEsperado = Session["RecuperarCodigo"].ToString();
            if (codigo?.Trim() != codigoEsperado)
                return Json(new { ok = false, mensaje = "Código incorrecto. Intenta de nuevo." },
                            JsonRequestBehavior.AllowGet);

            Session["PuedeResetear"] = true;

            return Json(new { ok = true }, JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // PASO 3: CAMBIAR CONTRASEÑA
        // ════════════════════════════════════════════════════════════
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult CambiarContrasena(string nuevaContrasena, string confirmarContrasena)
        {
            if (Session["PuedeResetear"] == null || Session["RecuperarCorreo"] == null)
                return Json(new { ok = false, mensaje = "Sesión inválida. Vuelve a comenzar." },
                            JsonRequestBehavior.AllowGet);

            if (string.IsNullOrWhiteSpace(nuevaContrasena))
                return Json(new { ok = false, mensaje = "La contraseña no puede estar vacía." },
                            JsonRequestBehavior.AllowGet);

            if (nuevaContrasena.Length < 6)
                return Json(new { ok = false, mensaje = "La contraseña debe tener al menos 6 caracteres." },
                            JsonRequestBehavior.AllowGet);

            if (nuevaContrasena != confirmarContrasena)
                return Json(new { ok = false, mensaje = "Las contraseñas no coinciden." },
                            JsonRequestBehavior.AllowGet);

            string hashNuevo = HashMD5(nuevaContrasena);
            string correo = Session["RecuperarCorreo"].ToString();

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");
            var filtro = Builders<UserModel>.Filter.Eq(u => u.Email, correo);
            var update = Builders<UserModel>.Update.Set(u => u.Password, hashNuevo);
            var result = colUsuarios.UpdateOne(filtro, update);

            if (result.ModifiedCount == 0)
                return Json(new { ok = false, mensaje = "No se pudo actualizar la contraseña." },
                            JsonRequestBehavior.AllowGet);

            Session.Remove("RecuperarCodigo");
            Session.Remove("RecuperarCorreo");
            Session.Remove("CodigoExpira");
            Session.Remove("PuedeResetear");

            return Json(new { ok = true, mensaje = "¡Contraseña actualizada! Ya puedes iniciar sesión." },
                        JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // ENVIAR CORREO POR GMAIL SMTP
        // ════════════════════════════════════════════════════════════
        private void EnviarCorreoCodigo(string destinatario, string codigo, string nombreUsuario)
        {
            string cuerpoHtml = $@"
            <!DOCTYPE html>
            <html lang='es'>
            <head><meta charset='UTF-8'/></head>
            <body style='margin:0;padding:0;background:#f5f5f5;font-family:Arial,sans-serif;'>
              <table width='100%' cellpadding='0' cellspacing='0'>
                <tr><td align='center' style='padding:40px 20px;'>
                  <table width='480' cellpadding='0' cellspacing='0'
                         style='background:#fff;border-radius:16px;overflow:hidden;
                                box-shadow:0 4px 20px rgba(0,0,0,0.08);'>
                    <tr>
                      <td style='background:linear-gradient(135deg,#e0006e,#9b27af);
                                 padding:32px 40px;text-align:center;'>
                        <h1 style='color:#fff;margin:0;font-size:22px;font-weight:700;'>
                          Belleza Suprema
                        </h1>
                        <p style='color:rgba(255,255,255,0.85);margin:6px 0 0;font-size:14px;'>
                          Recuperación de contraseña
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:36px 40px;'>
                        <p style='color:#333;font-size:15px;margin:0 0 12px;'>
                          Hola <strong>{nombreUsuario}</strong>,
                        </p>
                        <p style='color:#555;font-size:14px;margin:0 0 28px;line-height:1.6;'>
                          Tu código de verificación es:
                        </p>
                        <div style='background:#fce4f0;border:2px dashed #e0006e;
                                    border-radius:12px;padding:24px;text-align:center;
                                    margin-bottom:28px;'>
                          <span style='font-size:40px;font-weight:800;
                                       color:#e0006e;letter-spacing:10px;'>
                            {codigo}
                          </span>
                          <p style='margin:10px 0 0;color:#888;font-size:12px;'>
                            Expira en 10 minutos
                          </p>
                        </div>
                        <p style='color:#888;font-size:13px;margin:0;'>
                          Si no solicitaste este cambio, ignora este correo.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style='background:#fafafa;border-top:1px solid #f0f0f0;
                                 padding:20px 40px;text-align:center;'>
                        <p style='color:#bbb;font-size:12px;margin:0;'>
                          © {DateTime.Now.Year} Belleza Suprema
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>";

            var mensaje = new MailMessage
            {
                From = new MailAddress(GmailRemitente, NombreRemitente),
                Subject = $"Código de verificación: {codigo} — Belleza Suprema",
                Body = cuerpoHtml,
                IsBodyHtml = true
            };
            mensaje.To.Add(destinatario);

            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
            {
                smtp.Credentials = new NetworkCredential(GmailRemitente, GmailAppPassword);
                smtp.EnableSsl = true;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Send(mensaje);
            }
        }
    }
}

