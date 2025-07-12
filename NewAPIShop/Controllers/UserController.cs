using Microsoft.AspNetCore.Mvc;
using NewAPIShop.DataBase;
using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace NewAPIShop.Controllers
{
	public class UserController : Controller
	{
		private readonly ProductshopwmContext _context = new ProductshopwmContext();
		public UserController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpGet("Login")]
		public IActionResult Login(string login, string password)
		{
			if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
			{
				return BadRequest(new { message = "Логин и пароль не могут быть пустыми." });
			}

			try
			{
				string hashedPassword = ComputeSha256Hash(password.Trim());

				var user = _context.Users.AsNoTracking().FirstOrDefault(x => (x.Login == login || x.Email == login) && x.Password == hashedPassword);

				if (user == null /*|| user1 == null*/)
				{
					return Unauthorized(new { message = "Неправильный логин или пароль." });
				}

				var json = JsonConvert.SerializeObject(user, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
				return Ok(json);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpPost("CreateUser")]
		public IActionResult CreateUser(string login, string password, string repeatpassword, string email)
		{
			if (password != password)
			{
				return BadRequest(new { message = "Пароли не совподают" });
			}
			if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
			{
				return BadRequest(new { message = "Некоторые обязательные поля пусты." });
			}

			if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
			{
				return BadRequest(new { message = "Некорректный формат электронной почты." });
			}
			try
			{
				if (_context.Users.Any(x => x.Login == login || x.Email == email))
				{
					return Conflict(new { message = "Логин или почта уже используется." });
				}
				int userId = _context.Users.Any() ? _context.Users.Max(u => u.UserId) +1 : 1;
				var newUser = new User
				{
					UserId = userId,
					Login = login,
					Password = ComputeSha256Hash(password),
					Email = email,
					RoleId = 1
				};

				_context.Users.Add(newUser);
				_context.SaveChanges();

				var json = JsonConvert.SerializeObject(newUser, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
				return Ok(json);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}

		[HttpPost("UploadUserImage")]
		public IActionResult UploadUserImage([FromForm] int userId, [FromForm] IFormFile image)
		{
			if (image == null || image.Length == 0)
			{
				return BadRequest(new { message = "Изображение не выбрано." });
			}

			try
			{

				var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
				if (user == null)
				{
					return NotFound(new { message = "Пользователь не найден." });
				}

				var fileExtension = Path.GetExtension(image.FileName).ToLower();
				if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png")
				{
					return BadRequest(new { message = "Недопустимый формат изображения." });
				}

				using (var memoryStream = new MemoryStream())
				{
					image.CopyTo(memoryStream);
					user.ImageUser = memoryStream.ToArray();
				}

				_context.SaveChanges();

				return Ok(new { message = "Изображение успешно загружено." });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		private string ComputeSha256Hash(string password)
		{
			using (SHA256 sha256Hash = SHA256.Create())
			{
				byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
				return BitConverter.ToString(bytes).Replace("-", "").ToLower();
			}
		}

		[HttpGet("GetUserByEmail")]
		public IActionResult GetUserByEmail(string email)
		{
			var user = _context.Users.FirstOrDefault(x => x.Email == email);
			if (user == null)
			{
				return NotFound("Пользователь не найден.");
			}

			var json = JsonConvert.SerializeObject(user, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
			return Ok(json);
		}

		[HttpGet("GetUserByLogin")]
		public IActionResult GetUserByLogin(string login)
		{
			var user = _context.Users.FirstOrDefault(x => x.Login == login);
			if (user == null)
			{
				return NotFound("Пользователь не найден.");
			}

			// Возвращаем объект пользователя как JSON
			return Ok(user);  // Вместо сериализации в строку, возвращаем объект напрямую
		}
		[HttpPost("UpdateUserData")]
		public IActionResult UpdateUserData(int idUser, string login,string surname, string password, string email)
		{
			if (string.IsNullOrWhiteSpace(login) ||
				string.IsNullOrWhiteSpace(password) ||
				string.IsNullOrWhiteSpace(surname) ||
				string.IsNullOrWhiteSpace(email))
			{
				return BadRequest(new { message = "Все поля обязательны для заполнения." });
			}

			var user = _context.Users.FirstOrDefault(u => u.UserId == idUser);
			if (user == null)
			{
				return NotFound(new { message = "Пользователь не найден." });
			}

			user.Name = login; // или user.Login = login; — если у тебя есть отдельное поле Login
			user.Surname = surname;
			user.Password = ComputeSha256Hash(password);
			user.Email = email;

			_context.SaveChanges();

			return Ok(new { message = "Данные пользователя успешно обновлены." });
		}

		public static class CodeStorage
		{
			public static Dictionary<string, int> ConfirmationCodes = new Dictionary<string, int>();
		}

		[HttpPost("SendCode")]
		public IActionResult SendCode(string email)
		{
			if (string.IsNullOrEmpty(email))
			{
				return BadRequest("Email не может быть пустым.");
			}

			Random random = new Random();
			int confirmationCode = random.Next(1000, 9999);

			CodeStorage.ConfirmationCodes[email] = confirmationCode;

			try
			{
				SendEmail(email, confirmationCode);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Ошибка при отправке кода: {ex.Message}");
			}

			return Ok("Код подтверждения отправлен на почту.");
		}
		[HttpPost("ConfirmCode")]
		public IActionResult ConfirmCode(string email, int enteredCode)
		{
			if (string.IsNullOrEmpty(email))
			{
				return BadRequest("Email не может быть пустым.");
			}

			if (!CodeStorage.ConfirmationCodes.ContainsKey(email))
			{
				return BadRequest("Код подтверждения для данного email не найден.");
			}

			if (CodeStorage.ConfirmationCodes[email] == enteredCode)
			{
				CodeStorage.ConfirmationCodes.Remove(email);
				return Ok("Код подтвержден.");
			}

			return BadRequest("Неверный код подтверждения.");
		}

		[HttpPost("ConfirmCodeForNewPassword")]
		public IActionResult ConfirmCodeForNewPassword(string email, int enteredCode,string newpassword)
		{
			if (string.IsNullOrEmpty(email))
			{
				return BadRequest("Email не может быть пустым.");
			}

			if (!CodeStorage.ConfirmationCodes.ContainsKey(email))
			{
				return BadRequest("Код подтверждения для данного email не найден.");
			}

			if (CodeStorage.ConfirmationCodes[email] == enteredCode)
			{
				CodeStorage.ConfirmationCodes.Remove(email);
				var user = _context.Users.FirstOrDefault(x => x.Email == email);
				string password = ComputeSha256Hash(newpassword.Trim());
				if (user != null)
				{
					user.Password = password;
				}
				_context.SaveChanges();
				return Ok("Код подтвержден.");
			}

			return BadRequest("Неверный код подтверждения.");
		}
		private void SendEmail(string email, int confirmationCode)
		{
			MailAddress fromEmail = new MailAddress("mbydin@mail.ru", "Maksim");
			MailAddress toEmail = new MailAddress(email);
			MailMessage mail = new MailMessage(fromEmail, toEmail)
			{
				Subject = "Код-подтверждения",
				Body = $"<h2>Ваш код подтверждения - {confirmationCode}</h2>",
				IsBodyHtml = true
			};

			SmtpClient smtpClient = new SmtpClient("smtp.mail.ru", 587)
			{
				Credentials = new NetworkCredential("mbydin@mail.ru", "vAhkCaFp634Uyjww4htz"),
				EnableSsl = true
			};

			smtpClient.Send(mail);
		}
	}
}
