using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class CategoryController : Controller
	{
		private readonly ProductshopwmContext _context;

		public CategoryController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpGet("GetAllCategories")]
		public IActionResult GetAllCategories()
		{
			try
			{
				var categories = _context.Categories.ToList();
				if (!categories.Any())
				{
					return NotFound(new { message = "Категории отсутствуют." });
				}

				return Ok(categories);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при получении категорий.", error = ex.Message });
			}
		}
		[HttpGet("GetProductsByCategory")]
		public IActionResult GetProductsByCategory(int categoryId)
		{
			if (categoryId <= 0)
			{
				return BadRequest(new { message = "Некорректный ID категории." });
			}

			try
			{
				var products = _context.Products
					.Where(p => p.CategoryId == categoryId)
					.ToList();

				if (!products.Any())
				{
					return NotFound(new { message = "Товары в данной категории не найдены." });
				}

				return Ok(products);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при получении товаров.", error = ex.Message });
			}
		}
		[HttpPost("AddCategory")]
		public IActionResult AddCategory(string categoryName)
		{
			if (string.IsNullOrWhiteSpace(categoryName))
			{
				return BadRequest(new { message = "Название категории не может быть пустым." });
			}

			try
			{
				// Проверка на дубликат
				if (_context.Categories.Any(c => c.CategoryName.ToLower() == categoryName.ToLower()))
				{
					return Conflict(new { message = "Такая категория уже существует." });
				}

				var category = new Category { CategoryName = categoryName };
				_context.Categories.Add(category);
				_context.SaveChanges();

				return Ok(new { message = "Категория успешно добавлена." });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при добавлении категории.", error = ex.Message });
			}
		}
		[HttpDelete("DeleteCategory")]
		public IActionResult DeleteCategory(int categoryId)
		{
			if (categoryId <= 0)
			{
				return BadRequest(new { message = "Некорректный ID категории." });
			}

			try
			{
				var category = _context.Categories.FirstOrDefault(c => c.CategoryId == categoryId);
				if (category == null)
				{
					return NotFound(new { message = "Категория не найдена." });
				}

				// Проверка наличия товаров в категории
				if (_context.Products.Any(p => p.CategoryId == categoryId))
				{
					return Conflict(new
					{
						message = "Невозможно удалить категорию, так как в ней есть товары.",
						hasProducts = true
					});
				}

				_context.Categories.Remove(category);
				_context.SaveChanges();

				return Ok(new { message = "Категория успешно удалена." });
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError, new
				{
					message = "Ошибка при удалении категории.",
					error = ex.Message
				});
			}
		}
		[HttpPut("UpdateCategory")]
		public IActionResult UpdateCategory(int categoryId, string newCategoryName)
		{
			if (categoryId <= 0 || string.IsNullOrWhiteSpace(newCategoryName))
			{
				return BadRequest(new { message = "Некорректные данные." });
			}

			try
			{
				var category = _context.Categories.FirstOrDefault(c => c.CategoryId == categoryId);

				if (category == null)
				{
					return NotFound(new { message = "Категория не найдена." });
				}

				// Проверка на дубликат
				if (_context.Categories.Any(c =>
					c.CategoryName.ToLower() == newCategoryName.ToLower() &&
					c.CategoryId != categoryId))
				{
					return Conflict(new { message = "Категория с таким названием уже существует." });
				}

				category.CategoryName = newCategoryName;
				_context.SaveChanges();

				return Ok(new { message = "Категория успешно обновлена." });
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError, new
				{
					message = "Ошибка при обновлении категории.",
					error = ex.Message
				});
			}
		}
	}
}
