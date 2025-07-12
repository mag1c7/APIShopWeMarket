using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class FavoriteController : Controller
	{
		private readonly ProductshopwmContext _context;

		public FavoriteController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpGet("AddToFavorite")]
		public async Task<IActionResult> AddToFavorite(int userId, int productId)
		{
			// 1. Проверка входных данных
			if (userId <= 0 || productId <= 0)
			{
				return BadRequest(new
				{
					success = false,
					message = "Некорректные данные: userId или productId отсутствуют или некорректны."
				});
			}

			try
			{
				// 2. Проверяем, есть ли такой пользователь и товар
				var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
				if (!userExists)
				{
					return NotFound(new
					{
						success = false,
						message = "Пользователь не найден."
					});
				}

				var productExists = await _context.Products.AnyAsync(p => p.ProductId == productId);
				if (!productExists)
				{
					return NotFound(new
					{
						success = false,
						message = "Товар не найден."
					});
				}

				// 3. Проверяем, уже добавлен ли товар в избранное
				var existing = await _context.Favorites
					.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

				if (existing != null)
				{
					return Ok(new
					{
						success = true,
						message = "Товар уже в избранном."
					});
				}

				// 4. Добавляем товар в избранное
				var favorite = new Favorite
				{
					UserId = userId,
					ProductId = productId,
					AddedDate = DateTime.Now
				};

				_context.Favorites.Add(favorite);
				await _context.SaveChangesAsync();

				return Ok(new
				{
					success = true,
					message = "Товар успешно добавлен в избранное.",
					data = new
					{
						favoriteId = favorite.FavoriteId,
						favorite.AddedDate
					}
				});
			}
			catch (Exception ex)
			{
				// 5. Логируем ошибку (можно использовать ILogger, если настроен)
				return StatusCode(500, new
				{
					success = false,
					message = "Внутренняя ошибка сервера.",
					error = ex.Message
				});
			}
		}

		[HttpGet("RemoveFromFavorite")]
		public async Task<IActionResult> RemoveFromFavorite(int userId, int productId)
		{
			// 1. Проверка входных данных
			if (userId <= 0 || productId <= 0)
			{
				return BadRequest(new
				{
					success = false,
					message = "Некорректные данные: userId или productId отсутствуют или некорректны."
				});
			}

			try
			{
				// 2. Проверяем, есть ли такой пользователь
				var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
				if (!userExists)
				{
					return NotFound(new
					{
						success = false,
						message = "Пользователь не найден."
					});
				}

				// 3. Проверяем, существует ли товар
				var productExists = await _context.Products.AnyAsync(p => p.ProductId == productId);
				if (!productExists)
				{
					return NotFound(new
					{
						success = false,
						message = "Товар не найден."
					});
				}

				// 4. Проверяем, есть ли товар в избранном
				var existing = await _context.Favorites
					.FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId);

				if (existing == null)
				{
					return NotFound(new
					{
						success = false,
						message = "Товар не найден в избранном."
					});
				}

				// 5. Удаляем из избранного
				_context.Favorites.Remove(existing);
				await _context.SaveChangesAsync();

				return Ok(new
				{
					success = true,
					message = "Товар успешно удален из избранного."
				});
			}
			catch (Exception ex)
			{
				// 6. Обработка ошибок
				return StatusCode(500, new
				{
					success = false,
					message = "Внутренняя ошибка сервера.",
					error = ex.Message
				});
			}
		}

		[HttpGet("GetAllFavorites")]
		public async Task<IActionResult> GetAllFavorites(int userId)
		{
			if (userId <= 0)
				return BadRequest("Некорректный ID пользователя");

			var favorites = await _context.Favorites
				.Where(f => f.UserId == userId)
				.Select(f => f.ProductId)
				.ToListAsync();

			return Ok(favorites);
		}
		[HttpGet("CheckIfFavorited")]
		public async Task<IActionResult> CheckIfFavorited(int userId, int productId)
		{
			if (userId <= 0 || productId <= 0)
				return BadRequest(new { success = false, message = "Некорректные данные." });

			try
			{
				var isFavorited = await _context.Favorites
					.AnyAsync(f => f.UserId == userId && f.ProductId == productId);

				return Ok(new
				{
					success = true,
					isFavorited
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					success = false,
					message = "Ошибка сервера.",
					error = ex.Message
				});
			}
		}
		[HttpGet("GetAllFavoritesWithSorting")]
		public async Task<IActionResult> GetAllFavoritesWithSorting(int userId,string sortByField = "addedDate",string sortOrder = "desc")
		{
			if (userId <= 0)
			{
				return BadRequest(new { success = false, message = "Некорректный ID пользователя" });
			}

			var allowedSortFields = new[] { "addedDate", "price", "stock" };
			if (!allowedSortFields.Contains(sortByField))
			{
				return BadRequest(new { success = false, message = $"Поле сортировки должно быть одним из: {string.Join(", ", allowedSortFields)}" });
			}

			// Меняем тип на IQueryable<Favorite>
			IQueryable<Favorite> query = _context.Favorites
				.Where(f => f.UserId == userId)
				.Include(f => f.Product);

			// Применение сортировки
			switch (sortByField)
			{
				case "addedDate":
					query = sortOrder == "asc"
						? query.OrderBy(f => f.AddedDate)
						: query.OrderByDescending(f => f.AddedDate);
					break;

				case "price":
					query = sortOrder == "asc"
						? query.OrderBy(f => f.Product.Price)
						: query.OrderByDescending(f => f.Product.Price);
					break;

				case "stock":
					query = sortOrder == "asc"
						? query.OrderBy(f => f.Product.Stock)
						: query.OrderByDescending(f => f.Product.Stock);
					break;
			}

			try
			{
				var favorites = await query.ToListAsync();

				var result = favorites.Select(f => new
				{
					f.ProductId,
					ProductName = f.Product.Name,
					f.Product.Price,
					Quantity = f.Product.Stock,
					f.AddedDate
				}).ToList();

				return Ok(new { success = true, data = result });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					success = false,
					message = "Ошибка при загрузке данных.",
					error = ex.Message
				});
			}
		}
	}
}
