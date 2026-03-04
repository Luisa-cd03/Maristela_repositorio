using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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

        // ── MÉTODOS PARA EL ADMIN ──────────────────────────────────

        // Filtra citas por fecha y/o estado
        // Si fecha está vacío → no filtra por fecha
        // Si estado es "Todos" → no filtra por estado
        public static List<CitaModel> GetCitasFiltradas(string fecha, string estado)
        {
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

        public static ReportesViewModel GetEstadisticasReportes()
        {
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
    }
}
