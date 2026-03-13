// ============================================================
// ARCHIVO:     MongoDBHelper.cs
// UBICACIÓN:   Helpers/MongoDBHelper.cs
//
// CAMBIOS EN ESTA VERSIÓN:
//   ✦ AutoFinalizarCitasVencidas() — nuevo método que recorre
//     todas las citas Pendientes/Confirmadas y, si la fecha +
//     hora_fin ya pasó respecto a DateTime.Now (hora Colombia),
//     las actualiza a "Finalizada" en MongoDB.
//
//   ✦ GetCitasCliente() — nuevo método para el panel cliente.
//     Llama a AutoFinalizar antes de devolver, así el cliente
//     siempre ve el estado correcto.
//
//   ✦ GetCitasFiltradas() — también llama a AutoFinalizar,
//     así el admin siempre ve estados actualizados.
//
//   ✦ GetEstadisticasReportes() — también llama a AutoFinalizar
//     para que los contadores del dashboard sean precisos.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using System.Configuration;
using BellezaSuprema2.Models;

namespace BellezaSuprema2.Helpers
{
    public class MongoDBHelper
    {
        private static IMongoDatabase _database;

        public static IMongoDatabase GetDatabase()
        {
            if (_database == null)
            {
                var connectionString = ConfigurationManager.AppSettings["MongoDBConnection"];
                var settings = MongoClientSettings.FromConnectionString(connectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
                settings.ConnectTimeout = TimeSpan.FromSeconds(10);
                var client = new MongoClient(settings);
                _database = client.GetDatabase("DB-CiTA");
            }
            return _database;
        }

        public static IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            return GetDatabase().GetCollection<T>(collectionName);
        }

        // ════════════════════════════════════════════════════════════
        // AUTO-FINALIZACIÓN DE CITAS
        // ════════════════════════════════════════════════════════════
        //
        // Lógica:
        //   1. Trae todas las citas con estado "Pendiente" o "Confirmada"
        //   2. Para cada una, construye un DateTime combinando
        //      c.Fecha ("YYYY-MM-DD") + c.HoraFin ("HH:mm")
        //   3. Si ese DateTime ya pasó → actualiza estado a "Finalizada"
        //
        // Se llama automáticamente antes de leer citas en cualquier
        // método público de este helper.
        // ════════════════════════════════════════════════════════════
        public static void AutoFinalizarCitasVencidas()
        {
            try
            {
                var col = GetCollection<CitaModel>("Citas");
                var ahora = DateTime.Now; // hora del servidor (Colombia)

                // Traer solo citas activas (las canceladas/finalizadas se ignoran)
                var activas = col.Find(c =>
                    c.Estado == "Pendiente" || c.Estado == "Confirmada"
                ).ToList();

                foreach (var cita in activas)
                {
                    // Saltar si faltan datos de fecha u hora fin
                    if (string.IsNullOrEmpty(cita.Fecha) || string.IsNullOrEmpty(cita.HoraFin))
                        continue;

                    // Construir DateTime de fin: "2025-11-28" + "11:28" → 28/11/2025 11:28
                    DateTime fechaHoraFin;
                    bool parseOk = DateTime.TryParseExact(
                        cita.Fecha + " " + cita.HoraFin,
                        "yyyy-MM-dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out fechaHoraFin
                    );

                    if (!parseOk) continue;

                    // Si la cita ya terminó → marcar como Finalizada
                    if (fechaHoraFin <= ahora)
                    {
                        col.UpdateOne(
                            Builders<CitaModel>.Filter.Eq(c => c.Id, cita.Id),
                            Builders<CitaModel>.Update.Set(c => c.Estado, "Finalizada")
                        );
                    }
                }
            }
            catch
            {
                // Si falla la auto-finalización no se interrumpe el flujo normal
            }
        }

        // ════════════════════════════════════════════════════════════
        // MÉTODOS PARA EL ADMIN
        // ════════════════════════════════════════════════════════════

        // Filtra citas por fecha y/o estado.
        // Llama AutoFinalizar primero para que el admin siempre vea
        // estados actualizados sin tener que hacer nada manual.
        public static List<CitaModel> GetCitasFiltradas(string fecha, string estado)
        {
            AutoFinalizarCitasVencidas(); // ← NUEVO

            var colCitas = GetCollection<CitaModel>("Citas");
            var citas = colCitas.Find(_ => true).ToList();

            if (!string.IsNullOrEmpty(fecha))
                citas = citas.Where(c => c.Fecha == fecha).ToList();

            if (!string.IsNullOrEmpty(estado) && estado != "Todos")
                citas = citas.Where(c => c.Estado == estado).ToList();

            return citas;
        }

        // Retorna fechas únicas que tienen al menos una cita (para el calendario)
        public static List<string> GetFechasConCitas()
        {
            var colCitas = GetCollection<CitaModel>("Citas");
            return colCitas.Find(_ => true)
                           .ToList()
                           .Select(c => c.Fecha)
                           .Distinct()
                           .ToList();
        }

        // Retorna todos los usuarios (incluyendo admins) para cruzar nombres
        public static List<UserModel> GetTodosLosUsuarios()
        {
            var colUsuarios = GetCollection<UserModel>("Usuario");
            return colUsuarios.Find(_ => true).ToList();
        }

        // Retorna solo clientes (sin administradores) para la pestaña Usuarios
        public static List<UserModel> GetClientes()
        {
            var colUsuarios = GetCollection<UserModel>("Usuario");
            return colUsuarios.Find(_ => true)
                              .ToList()
                              .Where(u => u.Role != "Administrador")
                              .ToList();
        }

        // Estadísticas para el dashboard de reportes.
        // Llama AutoFinalizar para que los contadores sean exactos.
        public static ReportesViewModel GetEstadisticasReportes()
        {
            AutoFinalizarCitasVencidas(); // ← NUEVO

            var colCitas = GetCollection<CitaModel>("Citas");
            var colUsuarios = GetCollection<UserModel>("Usuario");
            var citas = colCitas.Find(_ => true).ToList();
            var usuarios = colUsuarios.Find(_ => true).ToList();

            return new ReportesViewModel
            {
                TotalCitas = citas.Count,
                CitasPendientes = citas.Count(c => c.Estado == "Pendiente"),
                CitasCanceladas = citas.Count(c => c.Estado == "Cancelada"),
                CitasFinalizadas = citas.Count(c => c.Estado == "Finalizada"),
                TotalUsuarios = usuarios.Count(u => u.Role != "Administrador"),
                IngresosEstimados = citas.Where(c => c.Estado == "Finalizada").Sum(c => c.Precio)
            };
        }

        // ════════════════════════════════════════════════════════════
        // MÉTODOS PARA EL PANEL CLIENTE
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Devuelve las citas de un usuario separadas en dos listas:
        ///   - Próximas: Pendiente o Confirmada con fecha >= hoy
        ///   - Historial: Finalizada, Cancelada, o cualquier cita pasada
        ///
        /// Llama AutoFinalizar antes de leer para que el cliente
        /// nunca vea una cita "Pendiente" que ya terminó.
        /// </summary>
        public static (List<CitaModel> proximas, List<CitaModel> historial, long total)
            GetCitasCliente(string idUsuario)
        {
            AutoFinalizarCitasVencidas(); // ← NUEVO: actualiza estados antes de leer

            var col = GetCollection<CitaModel>("Citas");
            var todas = col.Find(c => c.IdUsuario == idUsuario)
                           .ToList()
                           .OrderBy(c => c.Fecha)
                           .ThenBy(c => c.HoraInicio)
                           .ToList();

            var hoy = DateTime.Now.Date;

            // Próximas: estado activo Y fecha de HOY en adelante
            var proximas = todas.Where(c =>
                (c.Estado == "Pendiente" || c.Estado == "Confirmada") &&
                FechaToDate(c.Fecha) >= hoy
            ).ToList();

            // Historial: todo lo que no sea próximo
            var historial = todas.Where(c =>
                c.Estado == "Finalizada" ||
                c.Estado == "Cancelada" ||
                FechaToDate(c.Fecha) < hoy
            ).OrderByDescending(c => c.Fecha).ToList();

            return (proximas, historial, todas.Count);
        }

        // ── Helper privado: convierte "YYYY-MM-DD" a DateTime.Date ──
        private static DateTime FechaToDate(string fecha)
        {
            DateTime result;
            return DateTime.TryParseExact(
                fecha, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out result
            ) ? result.Date : DateTime.MinValue;
        }
    }
}
