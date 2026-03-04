using BellezaSuprema2.Helpers;
using BellezaSuprema2.Models;
using MongoDB.Driver;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;

namespace BellezaSuprema2.Controllers
{
    /// <summary>
    /// Controlador de autenticación y gestión de cuentas.
    /// Maneja: Login, Logout y Registro de nuevos usuarios.
    /// 
    /// Rutas:
    ///   GET  /Account/Login    → muestra formulario de login
    ///   POST /Account/Login    → valida credenciales contra MongoDB
    ///   GET  /Account/Register → muestra formulario de registro
    ///   POST /Account/Register → crea nuevo usuario en MongoDB
    ///   GET  /Account/Logout   → cierra sesión y redirige al login
    /// </summary>
    public class AccountController : Controller
    {
        // ════════════════════════════════════════════════════════════
        // MÉTODO PRIVADO: HashMD5
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Convierte una contraseña en texto plano a su equivalente MD5.
        /// Se usa porque en MongoDB las contraseñas están guardadas hasheadas.
        /// Ejemplo: "admin123" → "0192023a7bbd73250516f069df18b500"
        /// Se llama tanto en Login (para comparar) como en Register (para guardar).
        /// </summary>
        /// <param name="input">Texto plano a hashear</param>
        /// <returns>Hash MD5 en minúsculas sin guiones</returns>
        private string HashMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                // BitConverter genera "20-F3-76..." y Replace quita los guiones
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        // ════════════════════════════════════════════════════════════
        // LOGIN
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Account/Login
        /// Muestra la página de login.
        /// Si el usuario ya tiene sesión activa, lo redirige directo
        /// a su panel sin mostrar el formulario.
        /// </summary>
        [HttpGet]
        public ActionResult Login()
        {
            // Si ya hay sesión activa no tiene sentido mostrar el login
            if (Session["UserId"] != null)
            {
                if (Session["UserRole"]?.ToString() == "Administrador")
                    return RedirectToAction("Index", "Admin");
                else
                    return RedirectToAction("Index", "Home");
            }

            return View();
        }

        /// <summary>
        /// POST /Account/Login
        /// Recibe email y contraseña del formulario Login.cshtml.
        /// Hashea la contraseña y busca coincidencia en MongoDB.
        /// Si encuentra el usuario guarda sesión y redirige según rol.
        /// Si no, muestra mensaje de error en la vista.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken] // protege contra ataques CSRF
        public ActionResult Login(LoginViewModel model)
        {
            // Si los [Required] o [EmailAddress] del ViewModel fallaron,
            // regresa la vista con los mensajes de error sin tocar MongoDB
            if (!ModelState.IsValid)
                return View(model);

            // Hashea la contraseña ingresada para compararla con la de MongoDB
            string passwordHash = HashMD5(model.Password);

            // Obtiene la colección "Usuario" de DB-CiTA
            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");

            // Construye el filtro: busca donde Correo Y Contraseña coincidan.
            // Los nombres "Correo" y "Contraseña" deben coincidir exactamente
            // con los campos en Compass (con mayúscula y tilde incluida).
            var filtro = Builders<UserModel>.Filter.And(
                Builders<UserModel>.Filter.Eq(u => u.Email, model.Email),
                Builders<UserModel>.Filter.Eq(u => u.Password, passwordHash)

            );

            var user = colUsuarios.Find(filtro).FirstOrDefault();

            if (user != null)
            {
                // Login correcto: guarda datos del usuario en sesión.
                // Session persiste durante toda la navegación del usuario.
                Session["UserId"] = user.Id;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.Role;
                Session["Nombre"] = user.Nombre;

                // Redirige según el rol del usuario
                if (user.Role == "Administrador")
                    return RedirectToAction("Index", "Admin");
                else
                    return RedirectToAction("Index", "Home");
            }

            // Credenciales incorrectas: agrega error visible en la vista.
            // La clave "" significa error general (no ligado a un campo).
            ModelState.AddModelError("", "Correo o contraseña incorrectos.");
            return View(model);
        }

        // ════════════════════════════════════════════════════════════
        // REGISTER
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Account/Register
        /// Muestra el formulario para crear una nueva cuenta.
        /// Si ya hay sesión activa redirige directo al panel.
        /// </summary>
        [HttpGet]
        public ActionResult Register()
        {
            // Si ya hay sesión no tiene sentido mostrar el registro
            if (Session["UserId"] != null)
                return RedirectToAction("Index", "Admin");

            return View();
        }

        /// <summary>
        /// POST /Account/Register
        /// Recibe los datos del formulario Register.cshtml.
        /// 
        /// Flujo completo:
        /// 1. Valida que todos los campos requeridos estén completos
        /// 2. Verifica que el correo no esté ya registrado en MongoDB
        /// 3. Calcula el siguiente ID numérico disponible
        /// 4. Hashea la contraseña con MD5
        /// 5. Inserta el nuevo usuario en la colección "Usuario"
        /// 6. Redirige al login con mensaje de éxito
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            // Si los [Required] o [MinLength] fallaron,
            // regresa la vista mostrando los errores de validación
            if (!ModelState.IsValid)
                return View(model);

            var colUsuarios = MongoDBHelper.GetCollection<UserModel>("Usuario");

            // ── Verifica si el correo ya existe en MongoDB ──
            // Si ya hay un usuario con ese correo, no puede registrarse de nuevo
            var existe = colUsuarios.Find(
                 Builders<UserModel>.Filter.Eq(u => u.Email, model.Email)
             ).FirstOrDefault();

            if (existe != null)
            {
                // Agrega error general para mostrarlo en la vista
                ModelState.AddModelError("", "Este correo ya está registrado.");
                return View(model);
            }

            // ── Calcula el siguiente ID numérico disponible ──
            // Esta colección usa int como ID (no ObjectId).
            // Busca el usuario con el ID más alto y le suma 1.
            // Si no hay usuarios, empieza desde 1.
            //var ultimoUsuario = colUsuarios
            //   .Find(_ => true)
            //     .SortByDescending(u => u.Id)
            //    .FirstOrDefault();

            //int nuevoId = (ultimoUsuario?.Id ?? 0) + 1;


            // ── Construye el nuevo documento de usuario ──
            var nuevoUsuario = new UserModel
            {
                Nombre = model.Nombre,
                Email = model.Email,
                Telefono = model.Telefono,
                Password = HashMD5(model.Password),
                Role = "Usuario"
            };


            // ── Inserta en MongoDB ──
            // InsertOne agrega el documento a la colección "Usuario" de DB-CiTA
            colUsuarios.InsertOne(nuevoUsuario);

            // TempData persiste el mensaje solo para la siguiente petición (el redirect).
            // Después de eso se borra automáticamente.
            TempData["Exito"] = "¡Cuenta creada exitosamente! Ya puedes iniciar sesión.";

            // Redirige al login para que el usuario entre con su nueva cuenta
            return RedirectToAction("Login");
        }

        // ════════════════════════════════════════════════════════════
        // LOGOUT
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /Account/Logout
        /// Elimina todos los datos de sesión del usuario y redirige al login.
        /// Se llama desde el botón "Cerrar Sesión" del panel de admin.
        /// Session.Clear() borra: UserId, UserEmail, UserRole, Nombre.
        /// </summary>
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
     
        public JsonResult RefrescarToken()
        {
            // Genera el HTML del token antiforgery y extrae solo el value
            string html = System.Web.Helpers.AntiForgery.GetHtml().ToString();
            var match = System.Text.RegularExpressions.Regex.Match(html, @"value=""([^""]+)""");
            string valor = match.Success ? match.Groups[1].Value : "";
            return Json(new { token = valor }, JsonRequestBehavior.AllowGet);
        }

    }
}
