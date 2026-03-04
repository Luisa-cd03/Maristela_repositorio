// ============================================================
// ARCHIVO: UserModel.cs
// UBICACIÓN: Models/UserModel.cs
// DESCRIPCIÓN: Representa un documento de la colección "Usuario"
//              en MongoDB. [BsonIgnoreExtraElements] evita errores
//              si MongoDB tiene campos adicionales no definidos aquí.
// ============================================================
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BellezaSuprema2.Models
{
    [BsonIgnoreExtraElements]  // ← ignora campos extra de MongoDB (ej: creado_en, etc.)
    public class UserModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }
}