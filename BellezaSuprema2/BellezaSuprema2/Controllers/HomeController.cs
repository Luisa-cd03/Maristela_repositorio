// ============================================================
// REEMPLAZA COMPLETAMENTE el HomeController.cs
// Cambios clave:
//   - AgendarCita GET: solo carga servicios (sin cambios)
//   - ObtenerCupos: nuevo endpoint AJAX que devuelve horas
//     disponibles para un servicio+fecha con su conteo de cupos
//   - AgendarCita POST: valida que queden cupos antes de insertar
//   - CUPOS_POR_HORA = 5 trabajadores por franja horaria
// ============================================================
using BellezaSuprema2.Helpers;
using BellezaSuprema2.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace BellezaSuprema2.Controllers
{
    public class HomeController : Controller
    {
        // ── Cupos disponibles por franja horaria (trabajadores) ──
        private const int CUPOS_POR_HORA = 5;

        // ── Horario de atención: 07:00 a 18:00 en pasos de 30 min ──
        private static readonly List<string> HORAS_DISPONIBLES = GenerarHoras();

        private static List<string> GenerarHoras()
        {
            var horas = new List<string>();
            for (int h = 7; h <= 17; h++)
            {
                horas.Add($"{h:00}:00");
                horas.Add($"{h:00}:30");
            }
            horas.Add("18:00");
            return horas;
        }

        // ════════════════════════════════════════════════════════════
        // VERIFICAR SESIÓN
        // ════════════════════════════════════════════════════════════
        private bool VerificarSesion() => Session["UserId"] != null;

        // ════════════════════════════════════════════════════════════
        // INDEX — PANEL PRINCIPAL
        // ════════════════════════════════════════════════════════════
        public ActionResult Index()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            string userId = Session["UserId"].ToString();
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            string hoy = DateTime.Now.ToString("yyyy-MM-dd");

            // Próximas citas pendientes desde hoy
            var filtroPendientes = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente"),
                Builders<CitaModel>.Filter.Gte(c => c.Fecha, hoy)
            );

            var proximasCitas = colCitas.Find(filtroPendientes)
                .SortBy(c => c.Fecha).ThenBy(c => c.HoraInicio).ToList();

            // Historial: finalizadas, canceladas, vencidas
            var filtroHistorial = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.In(c => c.Estado,
                    new[] { "Finalizada", "Cancelada", "Vencida" })
            );

            var historialCitas = colCitas.Find(filtroHistorial)
                .SortByDescending(c => c.Fecha).ToList();

            long totalCitas = colCitas.CountDocuments(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId));

            ViewBag.ProximasCitas = proximasCitas;
            ViewBag.HistorialCitas = historialCitas;
            ViewBag.TotalCitas = totalCitas;
            ViewBag.Nombre = Session["Nombre"]?.ToString() ?? "Cliente";

            return View();
        }

        // ════════════════════════════════════════════════════════════
        // AGENDAR CITA — GET
        // Carga todos los servicios activos para mostrar las cards
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public ActionResult AgendarCita()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            var colServicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var servicios = colServicios
                .Find(Builders<ServicioModel>.Filter.Eq(s => s.Activo, true))
                .SortBy(s => s.Nombre)
                .ToList();

            ViewBag.Servicios = servicios;
            return View();
        }

        // ════════════════════════════════════════════════════════════
        // OBTENER CUPOS — GET (AJAX)
        // Recibe: servicioId (int) + fecha (yyyy-MM-dd)
        // Devuelve JSON con cada hora y cuántos cupos quedan.
        // Lógica: cuenta citas Pendientes que se solapan con cada
        // franja horaria y resta de CUPOS_POR_HORA (5).
        // ════════════════════════════════════════════════════════════
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult ObtenerCupos(int servicioId, string fecha)
        {
            if (!VerificarSesion())
                return Json(new { ok = false, mensaje = "Sesión expirada." },
                            JsonRequestBehavior.AllowGet);

            if (string.IsNullOrEmpty(fecha))
                return Json(new { ok = false, mensaje = "Fecha inválida." },
                            JsonRequestBehavior.AllowGet);

            // Validar que la fecha no sea en el pasado
            if (DateTime.TryParse(fecha, out DateTime fechaDt) && fechaDt.Date < DateTime.Now.Date)
                return Json(new { ok = false, mensaje = "No puedes agendar en fechas pasadas." },
                            JsonRequestBehavior.AllowGet);

            // Obtener el servicio para saber su duración
            var colServicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var servicio = colServicios.Find(
                Builders<ServicioModel>.Filter.Eq(s => s.Id, servicioId)
            ).FirstOrDefault();

            if (servicio == null)
                return Json(new { ok = false, mensaje = "Servicio no encontrado." },
                            JsonRequestBehavior.AllowGet);

            // Traer TODAS las citas Pendientes de esa fecha (cualquier servicio)
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var citasDelDia = colCitas.Find(
                Builders<CitaModel>.Filter.And(
                    Builders<CitaModel>.Filter.Eq(c => c.Fecha, fecha),
                    Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
                )
            ).ToList();

            // ── Citas que YA tiene el usuario ese día ──
            // Se usa para marcar esas horas como bloqueadas para él específicamente
            string userId = Session["UserId"].ToString();
            var citasDelUsuario = citasDelDia
                .Where(c => c.IdUsuario == userId)
                .ToList();

            // Para cada hora disponible calcular cuántos cupos quedan
            var resultado = new List<object>();

            foreach (var hora in HORAS_DISPONIBLES)
            {
                // Calcular hora de fin si el usuario tomara ESTE servicio en ESTA hora
                TimeSpan inicio = TimeSpan.Parse(hora);
                TimeSpan fin = inicio.Add(TimeSpan.FromMinutes(servicio.DuracionMin));

                // Si la cita terminaría después de las 19:00, no mostrar esa hora
                if (fin > TimeSpan.FromHours(19))
                    continue;

                // Si es hoy, no mostrar horas que ya pasaron
                if (fechaDt.Date == DateTime.Now.Date && inicio <= DateTime.Now.TimeOfDay)
                    continue;

                string horaFin = $"{(int)fin.TotalHours:00}:{fin.Minutes:00}";

                // ── Verificar si el usuario YA tiene una cita que se solape ──
                // Si tiene, esta hora se bloquea para él (independiente de cupos globales)
                bool usuarioYaTieneCita = citasDelUsuario.Any(c =>
                {
                    if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan cInicio)) return false;
                    if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cFin)) return false;
                    return cInicio < fin && cFin > inicio;
                });

                // Contar cuántas citas existentes se solapan con esta franja
                // Solapamiento: cita_inicio < mi_fin AND cita_fin > mi_inicio
                int ocupados = citasDelDia.Count(c =>
                {
                    if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan cInicio)) return false;
                    if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cFin)) return false;
                    return cInicio < fin && cFin > inicio;
                });

                int cuposLibres = CUPOS_POR_HORA - ocupados;

                resultado.Add(new
                {
                    hora = hora,
                    horaFin = horaFin,
                    cupos = cuposLibres,
                    // disponible = hay cupos globales Y el usuario no tiene ya una cita solapada
                    disponible = cuposLibres > 0 && !usuarioYaTieneCita,
                    // yaAgendado = el usuario ya tiene cita en este horario (mensaje especial)
                    yaAgendado = usuarioYaTieneCita
                });
            }

            return Json(new
            {
                ok = true,
                servicio = servicio.Nombre,
                duracion = servicio.DuracionMin,
                precio = servicio.PrecioBase,
                horas = resultado
            }, JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // AGENDAR CITA — POST
        // Recibe servicioId, fecha, horaInicio.
        // Revalida cupos en el servidor antes de insertar.
        // ════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgendarCita(int servicioId, string fecha, string horaInicio)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            // Validaciones básicas
            if (string.IsNullOrEmpty(fecha) || string.IsNullOrEmpty(horaInicio))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                return RedirectToAction("AgendarCita");
            }

            if (DateTime.TryParse(fecha, out DateTime fechaDt) &&
                fechaDt.Date < DateTime.Now.Date)
            {
                TempData["Error"] = "No puedes agendar en fechas pasadas.";
                return RedirectToAction("AgendarCita");
            }

            // Obtener servicio
            var colServicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var servicio = colServicios.Find(
                Builders<ServicioModel>.Filter.Eq(s => s.Id, servicioId)
            ).FirstOrDefault();

            if (servicio == null)
            {
                TempData["Error"] = "Servicio no encontrado.";
                return RedirectToAction("AgendarCita");
            }

            // Calcular hora fin
            TimeSpan inicio = TimeSpan.Parse(horaInicio);
            TimeSpan finSpan = inicio.Add(TimeSpan.FromMinutes(servicio.DuracionMin));
            string horaFin = $"{(int)finSpan.TotalHours:00}:{finSpan.Minutes:00}";

            // Revalidar cupos en servidor (evita race conditions)
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var citasDelDia = colCitas.Find(
                Builders<CitaModel>.Filter.And(
                    Builders<CitaModel>.Filter.Eq(c => c.Fecha, fecha),
                    Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
                )
            ).ToList();

            // ── Validación: el usuario NO puede tener otra cita solapada ese día ──
            string userId2 = Session["UserId"].ToString();
            bool usuarioConflicto = citasDelDia
                .Where(c => c.IdUsuario == userId2)
                .Any(c =>
                {
                    if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan cInicio)) return false;
                    if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cFin)) return false;
                    return cInicio < finSpan && cFin > inicio;
                });

            if (usuarioConflicto)
            {
                TempData["Error"] = "Ya tienes una cita agendada en ese horario. Por favor elige otro.";
                return RedirectToAction("AgendarCita");
            }

            int ocupados = citasDelDia.Count(c =>
            {
                if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan cInicio)) return false;
                if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cFin)) return false;
                return cInicio < finSpan && cFin > inicio;
            });

            if (ocupados >= CUPOS_POR_HORA)
            {
                TempData["Error"] = "Lo sentimos, ya no hay cupos disponibles para ese horario.";
                return RedirectToAction("AgendarCita");
            }

            // Crear e insertar la cita
            var nuevaCita = new CitaModel
            {
                IdUsuario = Session["UserId"].ToString(),
                ServicioId = servicio.Id,
                ServicioNombre = servicio.Nombre,
                Fecha = fecha,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                DuracionMin = servicio.DuracionMin,
                Precio = servicio.PrecioBase,
                EmpleadoId = 1,
                Estado = "Pendiente"
            };

            colCitas.InsertOne(nuevaCita);
            TempData["Exito"] = $"¡Cita agendada! {servicio.Nombre} el {fecha} a las {horaInicio}.";
            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // CANCELAR CITA
        // ════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelarCita(string citaId)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var filtro = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.Id, citaId),
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, Session["UserId"].ToString()),
                Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
            );
            var update = Builders<CitaModel>.Update.Set(c => c.Estado, "Cancelada");
            var resultado = colCitas.UpdateOne(filtro, update);

            TempData[resultado.ModifiedCount > 0 ? "Exito" : "Error"] =
                resultado.ModifiedCount > 0
                    ? "Cita cancelada correctamente."
                    : "No se pudo cancelar la cita.";

            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // CAMBIAR CONTRASEÑA
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public ActionResult CambiarContrasena()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContrasena(string contrasenaActual,
                                               string contrasenaNueva,
                                               string confirmarContrasena)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(contrasenaActual) ||
                string.IsNullOrEmpty(contrasenaNueva) ||
                string.IsNullOrEmpty(confirmarContrasena))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                return View();
            }

            if (contrasenaNueva != confirmarContrasena)
            {
                TempData["Error"] = "Las contraseñas nuevas no coinciden.";
                return View();
            }

            if (contrasenaNueva.Length < 6)
            {
                TempData["Error"] = "La contraseña debe tener al menos 6 caracteres.";
                return View();
            }

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");
            var filtro = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Eq(u => u.Id, Session["UserId"].ToString()),
                Builders<UserModel>.Filter.Eq(u => u.Password, HashMD5(contrasenaActual))
            );

            if (colUsuarios.Find(filtro).FirstOrDefault() == null)
            {
                TempData["Error"] = "La contraseña actual es incorrecta.";
                return View();
            }

            colUsuarios.UpdateOne(
                Builders<UserModel>.Filter.Eq(u => u.Id, Session["UserId"].ToString()),
                Builders<UserModel>.Update.Set(u => u.Password, HashMD5(contrasenaNueva))
            );

            TempData["Exito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index");
        }

        private string HashMD5(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}
