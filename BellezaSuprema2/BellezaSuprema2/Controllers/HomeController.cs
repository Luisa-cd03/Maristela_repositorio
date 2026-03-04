using BellezaSuprema2.Helpers;
using BellezaSuprema2.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace BellezaSuprema2.Controllers
{
    /// <summary>
    /// Controlador del panel del cliente (usuario normal).
    /// Maneja: Dashboard, Agendar cita, Cancelar cita, Cambiar contraseña.
    /// Todas las acciones requieren sesión activa de rol "Usuario".
    /// </summary>
    public class HomeController : Controller
    {
        // ════════════════════════════════════════════════════════════
        // FILTRO DE AUTENTICACIÓN PRIVADO
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Verifica que haya una sesión activa de usuario.
        /// Si no hay sesión, redirige al login.
        /// Se llama al inicio de cada acción protegida.
        /// </summary>
        private bool VerificarSesion()
        {
            return Session["UserId"] != null;
        }

        // ════════════════════════════════════════════════════════════
        // INDEX — PANEL PRINCIPAL DEL CLIENTE
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Home/Index
        /// Muestra el panel principal del cliente.
        /// Carga las próximas citas y el historial desde MongoDB.
        /// </summary>
        public ActionResult Index()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            string userId = Session["UserId"].ToString();
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");

            // Fecha de hoy en formato "YYYY-MM-DD" para comparar con los strings de MongoDB
            string hoy = DateTime.Now.ToString("yyyy-MM-dd");

            // ── Próximas citas: estado Pendiente y fecha >= hoy ──
            var filtroPendientes = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente"),
                Builders<CitaModel>.Filter.Gte(c => c.Fecha, hoy)
            );

            var proximasCitas = colCitas
                .Find(filtroPendientes)
                .SortBy(c => c.Fecha)
                .ThenBy(c => c.HoraInicio)
                .ToList();

            // ── Historial: citas Finalizadas, Canceladas o Vencidas ──
            var filtroHistorial = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId),
                Builders<CitaModel>.Filter.In(c => c.Estado, new[] { "Finalizada", "Cancelada", "Vencida" })
            );

            var historialCitas = colCitas
                .Find(filtroHistorial)
                .SortByDescending(c => c.Fecha)
                .ToList();

            // ── Total de citas del usuario (todas) ──
            var filtroTotal = Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, userId);
            long totalCitas = colCitas.CountDocuments(filtroTotal);

            // Pasar datos a la vista mediante ViewBag
            ViewBag.ProximasCitas = proximasCitas;
            ViewBag.HistorialCitas = historialCitas;
            ViewBag.TotalCitas = totalCitas;
            ViewBag.Nombre = Session["Nombre"]?.ToString() ?? "Cliente";

            return View();
        }

        // ════════════════════════════════════════════════════════════
        // AGENDAR CITA
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Home/AgendarCita
        /// Muestra el formulario para agendar una nueva cita.
        /// Carga la lista de servicios disponibles desde MongoDB.
        /// </summary>
        [HttpGet]
        public ActionResult AgendarCita()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            // Cargar servicios disponibles para el dropdown
            var colServicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var servicios = colServicios.Find(_ => true).SortBy(s => s.Nombre).ToList();
            ViewBag.Servicios = servicios;

            return View();
        }

        /// <summary>
        /// POST /Home/AgendarCita
        /// Recibe los datos del formulario y crea la cita en MongoDB.
        /// Valida que el horario no esté ocupado antes de insertar.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgendarCita(string servicioId, string fecha, string horaInicio)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(servicioId) || string.IsNullOrEmpty(fecha) || string.IsNullOrEmpty(horaInicio))
            {
                TempData["Error"] = "Todos los campos son obligatorios.";
                return RedirectToAction("AgendarCita");
            }

            // _id en Servicio es int
            int idServicioInt;
            if (!int.TryParse(servicioId, out idServicioInt))
            {
                TempData["Error"] = "Servicio no válido.";
                return RedirectToAction("AgendarCita");
            }

            // Obtener datos del servicio seleccionado
            var colServicios = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var servicio = colServicios.Find(
                Builders<ServicioModel>.Filter.Eq(s => s.Id, idServicioInt)
            ).FirstOrDefault();

            if (servicio == null)
            {
                TempData["Error"] = "Servicio no encontrado.";
                return RedirectToAction("AgendarCita");
            }

            // Calcular hora fin basado en duración del servicio
            TimeSpan horaInicioParsed = TimeSpan.Parse(horaInicio);
            TimeSpan horaFinParsed = horaInicioParsed.Add(TimeSpan.FromMinutes(servicio.DuracionMin));
            string horaFin = horaFinParsed.ToString(@"hh\:mm");

            // Verificar que el horario no esté ocupado ese día
            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");
            var citaExistente = colCitas.Find(
                Builders<CitaModel>.Filter.And(
                    Builders<CitaModel>.Filter.Eq(c => c.Fecha, fecha),
                    Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente"),
                    Builders<CitaModel>.Filter.Lt(c => c.HoraInicio, horaFin),
                    Builders<CitaModel>.Filter.Gt(c => c.HoraFin, horaInicio)
                )
            ).FirstOrDefault();

            if (citaExistente != null)
            {
                TempData["Error"] = "El horario seleccionado ya está ocupado. Por favor elige otro.";
                return RedirectToAction("AgendarCita");
            }

            // Crear la nueva cita
            var nuevaCita = new CitaModel
            {
                IdUsuario = Session["UserId"].ToString(),
                ServicioId = servicio.Id,          // int
                ServicioNombre = servicio.Nombre,
                Fecha = fecha,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                DuracionMin = servicio.DuracionMin,
                Precio = servicio.PrecioBase,       // precio_base → Precio en CitaModel
                EmpleadoId = 1,
                Estado = "Pendiente"
            };

            colCitas.InsertOne(nuevaCita);

            TempData["Exito"] = "¡Cita agendada exitosamente!";
            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // CANCELAR CITA
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /Home/CancelarCita
        /// Cambia el estado de una cita a "Cancelada".
        /// Solo permite cancelar citas que pertenezcan al usuario en sesión.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelarCita(string citaId)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(citaId))
            {
                TempData["Error"] = "Cita no válida.";
                return RedirectToAction("Index");
            }

            var colCitas = MongoDBHelper.GetCollection<CitaModel>("Citas");

            // Filtro doble: ID de cita + ID de usuario (seguridad: no puede cancelar citas ajenas)
            var filtro = Builders<CitaModel>.Filter.And(
                Builders<CitaModel>.Filter.Eq(c => c.Id, citaId),
                Builders<CitaModel>.Filter.Eq(c => c.IdUsuario, Session["UserId"].ToString()),
                Builders<CitaModel>.Filter.Eq(c => c.Estado, "Pendiente")
            );

            var update = Builders<CitaModel>.Update.Set(c => c.Estado, "Cancelada");
            var resultado = colCitas.UpdateOne(filtro, update);

            if (resultado.ModifiedCount > 0)
                TempData["Exito"] = "Cita cancelada correctamente.";
            else
                TempData["Error"] = "No se pudo cancelar la cita.";

            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // CAMBIAR CONTRASEÑA
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Home/CambiarContrasena
        /// Muestra el formulario para cambiar la contraseña.
        /// </summary>
        [HttpGet]
        public ActionResult CambiarContrasena()
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            return View();
        }

        /// <summary>
        /// POST /Home/CambiarContrasena
        /// Valida la contraseña actual y actualiza con la nueva en MongoDB.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContrasena(string contrasenaActual, string contrasenaNueva, string confirmarContrasena)
        {
            if (!VerificarSesion())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(contrasenaActual) || string.IsNullOrEmpty(contrasenaNueva) || string.IsNullOrEmpty(confirmarContrasena))
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
                TempData["Error"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                return View();
            }

            string hashActual = HashMD5(contrasenaActual);
            string hashNueva = HashMD5(contrasenaNueva);

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");

            // Verificar que la contraseña actual sea correcta
            var filtro = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Eq(u => u.Id, Session["UserId"].ToString()),
                Builders<UserModel>.Filter.Eq(u => u.Password, hashActual)
            );

            var usuario = colUsuarios.Find(filtro).FirstOrDefault();

            if (usuario == null)
            {
                TempData["Error"] = "La contraseña actual es incorrecta.";
                return View();
            }

            // Actualizar contraseña en MongoDB
            var update = Builders<UserModel>.Update.Set(u => u.Password, hashNueva);
            colUsuarios.UpdateOne(
                Builders<UserModel>.Filter.Eq(u => u.Id, Session["UserId"].ToString()),
                update
            );

            TempData["Exito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index");
        }

        // ════════════════════════════════════════════════════════════
        // MÉTODO PRIVADO: HashMD5 (igual que en AccountController)
        // ════════════════════════════════════════════════════════════
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