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
        private const int CUPOS_POR_HORA = 5;
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

        private bool VerificarSesion() => Session["UserId"] != null;

        // ════════════════════════════════════════════════════════════
        // INDEX — Panel del cliente
        // ════════════════════════════════════════════════════════════
        public ActionResult Index()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            string userId = Session["UserId"].ToString();

            // ── Auto-finalizar citas vencidas ANTES de leer ──────────
            // Esto actualiza en MongoDB todas las citas Pendientes/Confirmadas
            // cuya fecha+hora_fin ya pasó. El cliente siempre verá estados reales.
            MongoDBHelper.AutoFinalizarCitasVencidas();

            // ── Leer citas ya actualizadas ───────────────────────────
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            string hoy = DateTime.Now.ToString("yyyy-MM-dd");

            // Próximas: Pendiente O Confirmada, con fecha >= hoy
            var proximasCitas = colCitas.Find(Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.In(c => c.Estado, new[] { "Pendiente", "Confirmada" }),
                Builders<CitaModel>.Filter.Gte(c => c.Fecha, hoy)
            )).SortBy(c => c.Fecha).ThenBy(c => c.HoraInicio).ToList();

            // Historial: Finalizada, Cancelada, Vencida — orden descendente
            var historialCitas = colCitas.Find(Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.In(c => c.Estado, new[] { "Finalizada", "Cancelada", "Vencida" })
            )).SortByDescending(c => c.Fecha).ToList();

            // Total REAL de todas las citas del usuario (cualquier estado)
            long totalCitas = colCitas.CountDocuments(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId));

            // Favoritos ordenados por fecha de creación
            var favoritos = MongoDBHelper.GetCollection<FavoritoModel>("Favoritos")
                .Find(Builders<FavoritoModel>.Filter.Eq(f => f.IdUsuario, userId))
                .SortByDescending(f => f.CreadoEn)
                .ToList();

            ViewBag.ProximasCitas = proximasCitas;
            ViewBag.HistorialCitas = historialCitas;
            ViewBag.TotalCitas = totalCitas;
            ViewBag.Favoritos = favoritos;
            ViewBag.Nombre = Session["Nombre"]?.ToString() ?? "Cliente";

            return View();
        }

        // ════════════════════════════════════════════════════════════
        // AGENDAR CITA — GET
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public ActionResult AgendarCita()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            string userId = Session["UserId"].ToString();

            var servicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio")
                .Find(Builders<ServicioModel>.Filter.Eq(s => s.Activo, true))
                .SortBy(s => s.Nombre)
                .ToList();

            var favIds = MongoDBHelper.GetCollection<FavoritoModel>("Favoritos")
                .Find(Builders<FavoritoModel>.Filter.Eq(f => f.IdUsuario, userId))
                .Project(f => f.ServicioId)
                .ToList();

            ViewBag.Servicios = servicios;
            ViewBag.FavIds = favIds;
            return View();
        }

        // ════════════════════════════════════════════════════════════
        // OBTENER CUPOS — AJAX
        // ════════════════════════════════════════════════════════════
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public JsonResult ObtenerCupos(int servicioId, string fecha)
        {
            if (!VerificarSesion())
                return Json(new { ok = false, mensaje = "Sesión expirada." }, JsonRequestBehavior.AllowGet);

            if (string.IsNullOrEmpty(fecha))
                return Json(new { ok = false, mensaje = "Fecha inválida." }, JsonRequestBehavior.AllowGet);

            if (DateTime.TryParse(fecha, out DateTime fechaDt) && fechaDt.Date < DateTime.Now.Date)
                return Json(new { ok = false, mensaje = "No puedes agendar en fechas pasadas." }, JsonRequestBehavior.AllowGet);

            var servicio = MongoDBHelper.GetCollection<ServicioModel>("Servicio")
                .Find(Builders<ServicioModel>.Filter.Eq(s => s.Id, servicioId))
                .FirstOrDefault();

            if (servicio == null)
                return Json(new { ok = false, mensaje = "Servicio no encontrado." }, JsonRequestBehavior.AllowGet);

            var citasDelDia = MongoDBHelper.GetCollection<CitaModel>("Citas")
                .Find(Builders<CitaModel>.Filter.And(
                    Builders<CitaModel>.Filter.Eq(c => c.Fecha, fecha),
                    Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
                )).ToList();

            string userId = Session["UserId"].ToString();
            var citasDelUsuario = citasDelDia.Where(c => c.IdUsuario == userId).ToList();
            var resultado = new List<object>();

            foreach (var hora in HORAS_DISPONIBLES)
            {
                TimeSpan inicio = TimeSpan.Parse(hora);
                TimeSpan fin = inicio.Add(TimeSpan.FromMinutes(servicio.DuracionMin));

                if (fin > TimeSpan.FromHours(19)) continue;
                if (fechaDt.Date == DateTime.Now.Date && inicio <= DateTime.Now.TimeOfDay) continue;

                string horaFin = $"{(int)fin.TotalHours:00}:{fin.Minutes:00}";

                bool yaAgendado = citasDelUsuario.Any(c =>
                {
                    if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan ci)) return false;
                    if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cf)) return false;
                    return ci < fin && cf > inicio;
                });

                int ocupados = citasDelDia.Count(c =>
                {
                    if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan ci)) return false;
                    if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cf)) return false;
                    return ci < fin && cf > inicio;
                });

                resultado.Add(new
                {
                    hora = hora,
                    horaFin = horaFin,
                    cupos = CUPOS_POR_HORA - ocupados,
                    disponible = (CUPOS_POR_HORA - ocupados) > 0 && !yaAgendado,
                    yaAgendado = yaAgendado
                });
            }

            return Json(new { ok = true, horas = resultado }, JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // AGENDAR CITA — POST
        // ════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgendarCita(int servicioId, string fecha, string horaInicio)
        {
            if (!VerificarSesion()) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(fecha) || string.IsNullOrEmpty(horaInicio))
            { TempData["Error"] = "Todos los campos son obligatorios."; return RedirectToAction("AgendarCita"); }

            if (DateTime.TryParse(fecha, out DateTime fechaDt) && fechaDt.Date < DateTime.Now.Date)
            { TempData["Error"] = "No puedes agendar en fechas pasadas."; return RedirectToAction("AgendarCita"); }

            var servicio = MongoDBHelper.GetCollection<ServicioModel>("Servicio")
                .Find(Builders<ServicioModel>.Filter.Eq(s => s.Id, servicioId))
                .FirstOrDefault();

            if (servicio == null)
            { TempData["Error"] = "Servicio no encontrado."; return RedirectToAction("AgendarCita"); }

            TimeSpan inicio = TimeSpan.Parse(horaInicio);
            TimeSpan finSpan = inicio.Add(TimeSpan.FromMinutes(servicio.DuracionMin));
            string horaFin = $"{(int)finSpan.TotalHours:00}:{finSpan.Minutes:00}";

            string userId = Session["UserId"].ToString();
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var citasDelDia = colCitas.Find(Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.Fecha, fecha),
                Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
            )).ToList();

            bool conflicto = citasDelDia.Where(c => c.IdUsuario == userId).Any(c =>
            {
                if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan ci)) return false;
                if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cf)) return false;
                return ci < finSpan && cf > inicio;
            });

            if (conflicto)
            { TempData["Error"] = "Ya tienes una cita agendada en ese horario."; return RedirectToAction("AgendarCita"); }

            int ocupados = citasDelDia.Count(c =>
            {
                if (!TimeSpan.TryParse(c.HoraInicio, out TimeSpan ci)) return false;
                if (!TimeSpan.TryParse(c.HoraFin, out TimeSpan cf)) return false;
                return ci < finSpan && cf > inicio;
            });

            if (ocupados >= CUPOS_POR_HORA)
            { TempData["Error"] = "No hay cupos disponibles para ese horario."; return RedirectToAction("AgendarCita"); }

            colCitas.InsertOne(new CitaModel
            {
                IdUsuario = userId,
                ServicioId = servicio.Id,
                ServicioNombre = servicio.Nombre,
                Fecha = fecha,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                DuracionMin = servicio.DuracionMin,
                Precio = servicio.PrecioBase,
                EmpleadoId = 1,
                Estado = "Pendiente"
            });

            TempData["Exito"] = $"¡Cita agendada! {servicio.Nombre} el {fecha} a las {horaInicio}.";
            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // TOGGLE FAVORITO — AJAX POST
        // ════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleFavorito(int servicioId, string fotoUrl)
        {
            if (!VerificarSesion())
                return Json(new { ok = false, mensaje = "Sesión expirada." });

            string userId = Session["UserId"].ToString();
            var colFavs = MongoDBHelper.GetCollection<FavoritoModel>("Favoritos");

            var filtro = Builders<FavoritoModel>.Filter.And(
                Builders<FavoritoModel>.Filter.Eq(f => f.IdUsuario, userId),
                Builders<FavoritoModel>.Filter.Eq(f => f.ServicioId, servicioId)
            );

            var existente = colFavs.Find(filtro).FirstOrDefault();

            if (existente != null)
            {
                colFavs.DeleteOne(filtro);
                return Json(new { ok = true, esFavorito = false });
            }

            var servicio = MongoDBHelper.GetCollection<ServicioModel>("Servicio")
                .Find(Builders<ServicioModel>.Filter.Eq(s => s.Id, servicioId))
                .FirstOrDefault();

            if (servicio == null)
                return Json(new { ok = false, mensaje = "Servicio no encontrado." });

            colFavs.InsertOne(new FavoritoModel
            {
                IdUsuario = userId,
                ServicioId = servicio.Id,
                ServicioNombre = servicio.Nombre,
                Precio = servicio.PrecioBase,
                DuracionMin = servicio.DuracionMin,
                FotoUrl = fotoUrl ?? "",
                CreadoEn = DateTime.Now
            });

            return Json(new { ok = true, esFavorito = true });
        }

        // ════════════════════════════════════════════════════════════
        // CANCELAR CITA
        // ════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelarCita(string citaId)
        {
            if (!VerificarSesion()) return RedirectToAction("Login", "Account");

            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var resultado = colCitas.UpdateOne(
                Builders<CitaModel>.Filter.And(
                    Builders<CitaModel>.Filter.Eq(c => c.Id, citaId),
                    Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, Session["UserId"].ToString()),
                    Builders<CitaModel>.Filter.In(c => c.Estado, new[] { "Pendiente", "Confirmada" })
                ),
                Builders<CitaModel>.Update.Set(c => c.Estado, "Cancelada")
            );

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
            if (!VerificarSesion()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContrasena(string contrasenaActual, string contrasenaNueva, string confirmarContrasena)
        {
            if (!VerificarSesion()) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(contrasenaActual) || string.IsNullOrEmpty(contrasenaNueva) || string.IsNullOrEmpty(confirmarContrasena))
            { TempData["Error"] = "Todos los campos son obligatorios."; return View(); }

            if (contrasenaNueva != confirmarContrasena)
            { TempData["Error"] = "Las contraseñas nuevas no coinciden."; return View(); }

            if (contrasenaNueva.Length < 6)
            { TempData["Error"] = "La contraseña debe tener al menos 6 caracteres."; return View(); }

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");
            var filtro = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Eq(u => u.Id, Session["UserId"].ToString()),
                Builders<UserModel>.Filter.Eq(u => u.Password, HashMD5(contrasenaActual))
            );

            if (colUsuarios.Find(filtro).FirstOrDefault() == null)
            { TempData["Error"] = "La contraseña actual es incorrecta."; return View(); }

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
