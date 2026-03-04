// ============================================================
// ARCHIVO: ServicioModel.cs
// UBICACIÓN: Models/ServicioModel.cs
// ============================================================
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BellezaSuprema2.Models
{
    [BsonIgnoreExtraElements]   // ← ignora cualquier campo de MongoDB que no esté en el modelo
    public class ServicioModel
    {
        [BsonId]
        public int Id { get; set; }

        [BsonElement("nombre")]
        public string Nombre { get; set; }

        [BsonElement("descripcion")]
        public string Descripcion { get; set; }

        [BsonElement("duracion_min")]
        public int DuracionMin { get; set; }

        [BsonElement("precio_base")]
        public int PrecioBase { get; set; }

        [BsonElement("activo")]
        public bool Activo { get; set; }

        [BsonElement("creado_en")]
        public DateTime CreadoEn { get; set; }
    }
}
