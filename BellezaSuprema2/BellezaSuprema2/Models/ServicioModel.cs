// ============================================================
// ARCHIVO:     ServicioModel.cs
// UBICACIÓN:   Models/ServicioModel.cs
//
// CORRECCIÓN:  El _id en tu colección MongoDB "Servicio" es
//              Int32 (número entero), NO ObjectId.
//              Se elimina BsonRepresentation(BsonType.ObjectId).
//
// CAMPO NUEVO: ImagenUrl con [BsonIgnoreIfNull] para que los
//              documentos existentes que no tienen ese campo
//              no generen error al deserializar.
// ============================================================

using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BellezaSuprema2.Models
{
    public class ServicioModel
    {
        // ── Clave primaria: Int32 igual que en tu colección ────
        // Tu colección guarda _id como número entero, NO ObjectId.
        // NO uses [BsonRepresentation(BsonType.ObjectId)] aquí.
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

        // ── CAMPO NUEVO ────────────────────────────────────────
        // BsonIgnoreIfNull: si un documento en Mongo todavía no
        // tiene este campo, no lanzará error al leer.
        [BsonElement("imagen_url")]
        [BsonIgnoreIfNull]
        public string ImagenUrl { get; set; }

        [BsonElement("activo")]
        public bool Activo { get; set; }

        [BsonElement("creado_en")]
        public DateTime CreadoEn { get; set; }

        [BsonElement("actualizado_en")]
        [BsonIgnoreIfNull]
        public DateTime? ActualizadoEn { get; set; }
    }
}