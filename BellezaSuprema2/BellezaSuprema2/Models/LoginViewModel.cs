
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace BellezaSuprema2.Models
{
    /// <summary>
    /// ViewModel del formulario de login.
    /// Solo viaja entre la Vista y el Controlador, no se guarda en MongoDB.
    /// Los atributos [Required] y [EmailAddress] validan automáticamente con MVC.
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; }
    }
}