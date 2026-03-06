// ============================================================
// ARCHIVO: FavoritoModel.cs
// UBICACIÓN: Models/FavoritoModel.cs
// DESCRIPCIÓN: Representa un servicio marcado como favorito.
//              Colección "Favoritos" en MongoDB DB-CiTAS.
// ============================================================
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace BellezaSuprema2.Models
{
    [BsonIgnoreExtraElements]
    public class FavoritoModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("id_usuario")]
        public string IdUsuario { get; set; }

        [BsonElement("servicio_id")]
        public int ServicioId { get; set; }

        [BsonElement("servicio_nombre")]
        public string ServicioNombre { get; set; }

        [BsonElement("precio")]
        public int Precio { get; set; }

        [BsonElement("duracion_min")]
        public int DuracionMin { get; set; }

        [BsonElement("foto_url")]
        public string FotoUrl { get; set; }

        [BsonElement("creado_en")]
        public DateTime CreadoEn { get; set; }
    }
}
