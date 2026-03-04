// ============================================================
// ARCHIVO: AdminController.cs
// UBICACIÓN: Controllers/AdminController.cs
// DESCRIPCIÓN: Controlador principal del Panel de Administrador.
//              Maneja todas las peticiones HTTP que vienen desde
//              la vista Index.cshtml del Admin a través de AJAX.
//
//              RUTAS DISPONIBLES:
//              GET /Admin/Index          → Carga la vista principal del panel
//              GET /Admin/GetCitas       → Devuelve citas filtradas en JSON
//              GET /Admin/GetUsuarios    → Devuelve solo clientes en JSON
//              GET /Admin/GetTodosUsuarios → Devuelve todos los usuarios en JSON
//              GET /Admin/GetFechas      → Devuelve fechas con citas en JSON
// ============================================================

using System.Web.Mvc;               // Necesario para Controller, ActionResult, JsonResult
using BellezaSuprema2.Models;       // Necesario para CitaModel, UserModel, AdminDashboardViewModel
using BellezaSuprema2.Helpers;      // Necesario para MongoDBHelper (acceso a MongoDB)
using System.Collections.Generic;   // Necesario para usar List<>
using System.Linq;                   // Necesario para usar .Select(), .FirstOrDefault(), .ToList()

namespace BellezaSuprema2.Controllers
{
    public class AdminController : Controller
    {
        // ============================================================
        // ACCIÓN: Index
        // MÉTODO HTTP: GET
        // URL: /Admin/Index
        // DESCRIPCIÓN: Carga la vista principal del panel de administrador.
        //              La página se renderiza vacía y JavaScript se encarga
        //              de cargar los datos dinámicamente con AJAX.
        // ============================================================
        public ActionResult Index()
        {
            // Crea un ViewModel vacío y lo pasa a la vista
            // La vista Index.cshtml lo recibe como @model AdminDashboardViewModel
            return View(new AdminDashboardViewModel());
        }

        // ============================================================
        // ACCIÓN: GetCitas
        // MÉTODO HTTP: GET
        // URL: /Admin/GetCitas?fecha=2025-11-28&estado=Pendiente
        // DESCRIPCIÓN: Devuelve en formato JSON las citas filtradas
        //              por fecha y/o estado. También cruza el id_usuario
        //              de cada cita con la colección Usuario para
        //              mostrar el nombre real del cliente en la tabla.
        // PARÁMETROS:
        //   fecha  → Fecha en formato "YYYY-MM-DD" (opcional)
        //   estado → "Pendiente", "Finalizada", "Cancelada", "Vencida"
        //            o "Todos" para no filtrar por estado (opcional)
        // ============================================================
        public JsonResult GetCitas(string fecha, string estado)
        {
            // Obtiene las citas filtradas por fecha y/o estado desde MongoDB
            List<CitaModel> citas = MongoDBHelper.GetCitasFiltradas(fecha, estado);

            // Obtiene todos los usuarios (incluyendo admins) para cruzar el nombre
            // Se necesitan todos porque el id_usuario puede pertenecer a cualquier rol
            List<UserModel> usuarios = MongoDBHelper.GetTodosLosUsuarios();

            // Proyecta cada cita a un objeto anónimo con solo los campos que
            // la vista necesita, reemplazando IdUsuario por el nombre real
            var resultado = citas.Select(c => new
            {
                // Busca en la lista de usuarios el que tenga el mismo Id que c.IdUsuario
                // Si lo encuentra, toma su campo Nombre
                // Si no lo encuentra (usuario eliminado), muestra "Desconocido"
                NombreCliente = usuarios.FirstOrDefault(u => u.Id == c.IdUsuario) != null
                                 ? usuarios.FirstOrDefault(u => u.Id == c.IdUsuario).Nombre
                                 : "Desconocido",

                ServicioNombre = c.ServicioNombre, // Nombre del servicio (ej: "Uñas de gel")
                Fecha = c.Fecha,          // Fecha de la cita (ej: "2025-11-28")
                HoraInicio = c.HoraInicio,     // Hora de inicio (ej: "09:58")
                Precio = c.Precio,         // Precio en pesos (ej: 45000)
                Estado = c.Estado          // Estado actual (ej: "Pendiente")

            }).ToList(); // Convierte el resultado del Select a List

            // Retorna el resultado como JSON
            // JsonRequestBehavior.AllowGet permite responder peticiones GET con JSON
            // (ASP.NET MVC lo bloquea por defecto por seguridad)
            return Json(resultado, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // ACCIÓN: GetUsuarios
        // MÉTODO HTTP: GET
        // URL: /Admin/GetUsuarios
        // DESCRIPCIÓN: Devuelve en JSON solo los usuarios con rol
        //              distinto de "Administrador" (solo clientes).
        //              Se mantiene por compatibilidad con otras partes
        //              del sistema que puedan usarlo.
        // ============================================================
        public JsonResult GetUsuarios()
        {
            // Llama al helper que filtra y devuelve solo los clientes
            List<UserModel> usuarios = MongoDBHelper.GetClientes();

            // Retorna la lista de clientes en formato JSON
            return Json(usuarios, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // ACCIÓN: GetTodosUsuarios
        // MÉTODO HTTP: GET
        // URL: /Admin/GetTodosUsuarios
        // DESCRIPCIÓN: Devuelve en JSON TODOS los usuarios registrados,
        //              incluyendo administradores. Se usa en la pestaña
        //              "Usuarios" del panel para mostrar la lista completa
        //              con el buscador en tiempo real.
        // ============================================================
        public JsonResult GetTodosUsuarios()
        {
            // Llama al helper que devuelve todos los usuarios sin filtro de rol
            List<UserModel> usuarios = MongoDBHelper.GetTodosLosUsuarios();

            // Retorna todos los usuarios en formato JSON
            return Json(usuarios, JsonRequestBehavior.AllowGet);
        }

        // ============================================================
        // ACCIÓN: GetFechas
        // MÉTODO HTTP: GET
        // URL: /Admin/GetFechas
        // DESCRIPCIÓN: Devuelve en JSON la lista de fechas únicas que
        //              tienen al menos una cita agendada. El calendario
        //              en la vista usa este listado para mostrar un punto
        //              debajo de cada día que tiene citas.
        // ============================================================
        public JsonResult GetFechas()
        {
            // Llama al helper que extrae las fechas únicas de la colección Citas
            List<string> fechas = MongoDBHelper.GetFechasConCitas();

            // Retorna el array de fechas en JSON (ej: ["2025-11-28", "2025-12-17"])
            return Json(fechas, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetReportes()
        {
            var stats = MongoDBHelper.GetEstadisticasReportes();
            return Json(stats, JsonRequestBehavior.AllowGet);
        }
    }
}
