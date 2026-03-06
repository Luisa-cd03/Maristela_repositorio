// ============================================================
// ARCHIVO:     AdminController.cs
// UBICACIÓN:   Controllers/AdminController.cs
// DESCRIPCIÓN: Controlador principal del Panel de Administrador.
//
// CAMBIOS RESPECTO A LA VERSIÓN ANTERIOR:
//   • GuardarServicio  → acepta el parámetro imagenUrl
//   • EditarServicio   → acepta y actualiza imagenUrl
//   • GetServicios     → ahora expone ImagenUrl en la respuesta
//
// ENDPOINTS CRUD SERVICIOS:
//   GET  /Admin/GetServicios
//   POST /Admin/GuardarServicio
//   POST /Admin/EditarServicio
//   POST /Admin/EliminarServicio
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
        // ENDPOINTS EXISTENTES (sin cambios)
        // ════════════════════════════════════════════════════════════

        // GET /Admin/Reportes — Vista separada de reportes avanzados
        [HttpGet]
        public ActionResult Reportes()
        {
            return View();
        }

        public ActionResult Index()
        {
            return View(new AdminDashboardViewModel());
        }

        public JsonResult GetCitas(string fecha, string estado)
        {
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
            return Json(MongoDBHelper.GetEstadisticasReportes(), JsonRequestBehavior.AllowGet);
        }

        // ════════════════════════════════════════════════════════════
        // CRUD SERVICIOS
        // ════════════════════════════════════════════════════════════

        // ── GET /Admin/GetServicios ──────────────────────────────
        // Devuelve todos los servicios ordenados por nombre.
        // Expone ImagenUrl para que el panel admin pueda mostrar
        // la imagen en las tarjetas.
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
                s.ImagenUrl,   // ← CAMPO NUEVO
                s.Activo
            }).ToList();

            return Json(resultado, JsonRequestBehavior.AllowGet);
        }

        // ── POST /Admin/GuardarServicio ──────────────────────────
        // Crea un nuevo servicio.
        // Parámetros del body (application/x-www-form-urlencoded):
        //   nombre, descripcion, duracionMin, precioBase, activo, imagenUrl
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
                ImagenUrl = imagenUrl?.Trim() ?? "",   // ← CAMPO NUEVO
                Activo = activo,
                CreadoEn = DateTime.Now
            });

            return Json(new { ok = true, mensaje = "Servicio creado correctamente." });
        }

        // ── POST /Admin/EditarServicio ───────────────────────────
        // Actualiza un servicio existente.
        // Parámetros del body:
        //   id, nombre, descripcion, duracionMin, precioBase, activo, imagenUrl
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
                    .Set(s => s.ImagenUrl, imagenUrl?.Trim() ?? "")   // ← CAMPO NUEVO
                    .Set(s => s.Activo, activo)
                    .Set(s => s.ActualizadoEn, DateTime.Now)
            );

            if (result.ModifiedCount == 0)
                return Json(new { ok = false, mensaje = "No se encontró el servicio." });

            return Json(new { ok = true, mensaje = "Servicio actualizado correctamente." });
        }

        // ── POST /Admin/EliminarServicio ─────────────────────────
        // Elimina un servicio por su Id numérico.
        // Parámetro del body: id
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
