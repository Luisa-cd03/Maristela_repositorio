// ============================================================
// ARCHIVO: AdminDashboardViewModel.cs
// UBICACIÓN: Models/AdminDashboardViewModel.cs
// DESCRIPCIÓN: ViewModel que agrupa los datos necesarios para
//              mostrar el Panel de Administrador. Un ViewModel
//              no es una colección de MongoDB; es un modelo
//              intermedio que la Vista necesita para renderizarse.
// ============================================================

using System.Collections.Generic; // Necesario para usar List<>

namespace BellezaSuprema2.Models
{
    // Esta clase agrupa todo lo que la vista Index.cshtml del Admin necesita mostrar
    public class AdminDashboardViewModel
    {
        // Lista de citas que se mostrarán en la tabla de "Citas Programadas"
        // Se inicializa vacía para evitar errores de null en la vista
        public List<CitaModel> Citas { get; set; } = new List<CitaModel>();

        // Lista de usuarios que se mostrarán en la pestaña "Usuarios"
        // Solo contendrá usuarios con Role != "Administrador"
        public List<UserModel> Usuarios { get; set; } = new List<UserModel>();

        // Fecha seleccionada en el calendario (puede ser null si no se ha seleccionado ninguna)
        // Se usa para filtrar las citas por día específico
        public string FechaSeleccionada { get; set; }

        // Estado seleccionado en el combobox (ej: "Pendiente", "Finalizada", etc.)
        // Por defecto es "Todos" para mostrar todas las citas sin filtrar
        public string EstadoFiltro { get; set; } = "Todos";
    }
}