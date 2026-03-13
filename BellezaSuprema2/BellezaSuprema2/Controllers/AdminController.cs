// ============================================================
// ARCHIVO:     AdminController.cs
// UBICACIÓN:   Controllers/AdminController.cs
//
// CAMBIOS EN ESTA VERSIÓN:
//   ✦ GET /Admin/AutoFinalizarCitas — endpoint manual para que
//     el admin pueda forzar la actualización de estados desde
//     el panel (botón "Actualizar estados").
//
//   ✦ GetCitas ahora expone también HoraFin en la respuesta.
//
//   ✦ GetCitas y GetReportes llaman AutoFinalizar a través del
//     helper, así el admin siempre ve estados correctos.
// ============================================================

using System.Web.Mvc;
using BellezaSuprema2.Models;
using BellezaSuprema2.Helpers;
using System.Collections.Generic;
using System.Linq;
using System;
using MongoDB.Driver;

namespace BellezaSuprema2.Controllers
{
    public class AdminController : Controller
    {
        // ════════════════════════════════════════════════════════════
        // VISTAS
        // ════════════════════════════════════════════════════════════

        [HttpGet]
        public ActionResult Reportes()
        {
            return View();
        }

        public ActionResult Index()
        {
            return View(new AdminDashboardViewModel());
        }

        // ════════════════════════════════════════════════════════════
        // AUTO-FINALIZACIÓN MANUAL
        // GET /Admin/AutoFinalizarCitas
        // Devuelve { ok, finalizadas } — cantidad de citas actualizadas
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public JsonResult AutoFinalizarCitas()
        {
            try
            {
                var col = MongoDBHelper.GetCollection<CitaModel>("Citas");
                var ahora = DateTime.Now;

                var activas = col.Find(c =>
                    c.Estado == "Pendiente" || c.Estado == "Confirmada"
                ).ToList();

                int count = 0;
                foreach (var cita in activas)
                {
                    if (string.IsNullOrEmpty(cita.Fecha) || string.IsNullOrEmpty(cita.HoraFin))
                        continue;

                    DateTime fechaHoraFin;
                    bool ok = DateTime.TryParseExact(
                        cita.Fecha + " " + cita.HoraFin,
                        "yyyy-MM-dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out fechaHoraFin
                    );
                    if (!ok) continue;

                    if (fechaHoraFin <= ahora)
                    {
                        col.UpdateOne(
                            Builders<CitaModel>.Filter.Eq(c => c.Id, cita.Id),
                            Builders<CitaModel>.Update.Set(c => c.Estado, "Finalizada")
                        );
                        count++;
                    }
                }

                return Json(new { ok = true, finalizadas = count }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ════════════════════════════════════════════════════════════
        // ENDPOINTS DE DATOS
        // ════════════════════════════════════════════════════════════

        public JsonResult GetCitas(string fecha, string estado)
        {
            // AutoFinalizar se ejecuta dentro de GetCitasFiltradas
            List<CitaModel> citas = MongoDBHelper.GetCitasFiltradas(fecha, estado);
            List<UserModel> usuarios = MongoDBHelper.GetTodosLosUsuarios();

            var resultado = citas.Select(c => new
            {
                NombreCliente = usuarios.FirstOrDefault(u => u.Id == c.IdUsuario) != null
                                  ? usuarios.FirstOrDefault(u => u.Id == c.IdUsuario).Nombre
                                  : "Desconocido",
                ServicioNombre = c.ServicioNombre,
                Fecha = c.Fecha,
                HoraInicio = c.HoraInicio,
                HoraFin = c.HoraFin,
                Precio = c.Precio,
                Estado = c.Estado
            }).ToList();

            return Json(resultado, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetUsuarios()
        {
            return Json(MongoDBHelper.GetClientes(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetTodosUsuarios()
        {
            return Json(MongoDBHelper.GetTodosLosUsuarios(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetFechas()
        {
            return Json(MongoDBHelper.GetFechasConCitas(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetReportes()
        {
            // AutoFinalizar se ejecuta dentro de GetEstadisticasReportes
            return Json(MongoDBHelper.GetEstadisticasReportes(), JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // CRUD SERVICIOS
        // ════════════════════════════════════════════════════════════

        public JsonResult GetServicios()
        {
            var col = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var lista = col.Find(_ => true).SortBy(s => s.Nombre).ToList();

            var resultado = lista.Select(s => new {
                s.Id,
                s.Nombre,
                s.Descripcion,
                s.DuracionMin,
                s.PrecioBase,
                s.ImagenUrl,
                s.Activo
            }).ToList();

            return Json(resultado, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GuardarServicio(string nombre, string descripcion,
                                          int duracionMin, int precioBase,
                                          bool activo, string imagenUrl)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return Json(new { ok = false, mensaje = "El nombre es obligatorio." });

            var col = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var todos = col.Find(_ => true).ToList();
            int nuevoId = todos.Count > 0 ? todos.Max(s => s.Id) + 1 : 1;

            col.InsertOne(new ServicioModel
            {
                Id = nuevoId,
                Nombre = nombre.Trim(),
                Descripcion = descripcion?.Trim() ?? "",
                DuracionMin = duracionMin,
                PrecioBase = precioBase,
                ImagenUrl = imagenUrl?.Trim() ?? "",
                Activo = activo,
                CreadoEn = DateTime.Now
            });

            return Json(new { ok = true, mensaje = "Servicio creado correctamente." });
        }

        [HttpPost]
        public JsonResult EditarServicio(int id, string nombre, string descripcion,
                                         int duracionMin, int precioBase,
                                         bool activo, string imagenUrl)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return Json(new { ok = false, mensaje = "El nombre es obligatorio." });

            var col = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var result = col.UpdateOne(
                Builders<ServicioModel>.Filter.Eq(s => s.Id, id),
                Builders<ServicioModel>.Update
                    .Set(s => s.Nombre, nombre.Trim())
                    .Set(s => s.Descripcion, descripcion?.Trim() ?? "")
                    .Set(s => s.DuracionMin, duracionMin)
                    .Set(s => s.PrecioBase, precioBase)
                    .Set(s => s.ImagenUrl, imagenUrl?.Trim() ?? "")
                    .Set(s => s.Activo, activo)
                    .Set(s => s.ActualizadoEn, DateTime.Now)
            );

            if (result.ModifiedCount == 0)
                return Json(new { ok = false, mensaje = "No se encontró el servicio." });

            return Json(new { ok = true, mensaje = "Servicio actualizado correctamente." });
        }

        [HttpPost]
        public JsonResult EliminarServicio(int id)
        {
            var col = MongoDBHelper.GetCollection<ServicioModel>("Servicio");
            var result = col.DeleteOne(Builders<ServicioModel>.Filter.Eq(s => s.Id, id));

            if (result.DeletedCount == 0)
                return Json(new { ok = false, mensaje = "No se encontró el servicio." });

            return Json(new { ok = true, mensaje = "Servicio eliminado correctamente." });
        }
    }
}
