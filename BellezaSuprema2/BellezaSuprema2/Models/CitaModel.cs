// ============================================================
// ARCHIVO: CitaModel.cs
// UBICACIÓN: Models/CitaModel.cs
// DESCRIPCIÓN: Define la estructura de una Cita en MongoDB.
//              Cada propiedad corresponde a un campo del documento
//              en la colección "Citas" de la base de datos DB-CiTA.
// ============================================================

using MongoDB.Bson;                          // Necesario para trabajar con tipos de MongoDB como ObjectId
using MongoDB.Bson.Serialization.Attributes; // Necesario para los atributos [BsonId], [BsonElement], etc.
using System;                                // Necesario para usar el tipo DateTime

namespace BellezaSuprema2.Models
{
    // La clase CitaModel representa UN documento de la colección "Citas"
    public class CitaModel
    {
        // [BsonId] le dice a MongoDB que esta propiedad es el identificador único del documento (_id)
        [BsonId]
        // [BsonRepresentation] convierte automáticamente el ObjectId de MongoDB a string de C# y viceversa
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }       // Corresponde al campo "_id" en MongoDB

        // [BsonElement("campo")] mapea la propiedad de C# con el nombre exacto del campo en MongoDB
        [BsonElement("id_usuario")]
        public string IdUsuario { get; set; } // ID del usuario que agendó la cita (referencia a colección Usuario)

        [BsonElement("servicio_id")]
        public int ServicioId { get; set; }   // ID numérico del servicio seleccionado (referencia a colección Servicio)

        [BsonElement("servicio_nombre")]
        public string ServicioNombre { get; set; } // Nombre del servicio (ej: "Uñas de gel", "Tinte completo")

        [BsonElement("fecha")]
        public string Fecha { get; set; }     // Fecha de la cita en formato "YYYY-MM-DD" (ej: "2025-11-28")

        [BsonElement("hora_inicio")]
        public string HoraInicio { get; set; } // Hora de inicio de la cita (ej: "09:58")

        [BsonElement("hora_fin")]
        public string HoraFin { get; set; }    // Hora de fin de la cita (ej: "11:28")

        [BsonElement("duracion_min")]
        public int DuracionMin { get; set; }   // Duración del servicio en minutos (ej: 90)

        [BsonElement("precio")]
        public int Precio { get; set; }        // Precio del servicio en pesos (ej: 45000)

        [BsonElement("empleado_id")]
        public int EmpleadoId { get; set; }    // ID del empleado asignado a la cita

        [BsonElement("estado")]
        public string Estado { get; set; }     // Estado actual: "Pendiente", "Finalizada", "Cancelada", "Vencida"
    }
}
