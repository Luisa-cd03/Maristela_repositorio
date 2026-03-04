
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace BellezaSuprema2.Models
{
    /// <summary>
    /// ViewModel del formulario de registro.
    /// Solo transporta datos del formulario al controlador,
    /// no se guarda directamente en MongoDB.
    /// Los atributos [Required] validan automáticamente en MVC.
    /// </summary>
    public class RegisterViewModel
    {
        /// <summary>
        /// Nombre completo del nuevo usuario.
        /// Se guarda en MongoDB como campo "Nombre".
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; }

        /// <summary>
        /// Correo electrónico. Se valida formato con [EmailAddress].
        /// Se usa también para el login. Debe ser único en MongoDB.
        /// </summary>
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        public string Email { get; set; }

        /// <summary>
        /// Teléfono del usuario (ej: "3001234567").
        /// Se guarda en MongoDB como campo "Teléfono".
        /// </summary>
        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        public string Telefono { get; set; }

        /// <summary>
        /// Contraseña en texto plano. El controlador la
        /// convierte a MD5 antes de guardarla en MongoDB.
        /// Mínimo 4 caracteres por seguridad básica.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(4, ErrorMessage = "Mínimo 4 caracteres.")]
        public string Password { get; set; }
    }
}