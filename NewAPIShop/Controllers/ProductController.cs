using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class ProductController : Controller
	{
		private readonly ProductshopwmContext _context;

		public ProductController(ProductshopwmContext context)
		{
			_context = context;
		}
		[HttpPost("CreateProduct")]
		[Consumes("multipart/form-data")]
		public async Task<IActionResult> CreateProduct([FromForm] ProductFormModel model, List<IFormFile> images)
		{
			if (!ModelState.IsValid)
				return BadRequest("Некорректные данные.");

			try
			{
				// Создаем новый продукт
				var product = new Product
				{
					Name = model.Name,
					Description = model.Description,
					Price = model.Price,
					Stock = model.Stock,
					CategoryId = model.CategoryId,
					Supplier = model.Supplier,
					CountryOfOrigin = model.CountryOfOrigin,
					ExpirationDate = model.ExpirationDate.ToString()
				};

				_context.Products.Add(product);
				await _context.SaveChangesAsync();

				// Сохраняем изображения
				foreach (var image in images)
				{
					if (image != null)
					{
						var productImage = new ProductImage
						{
							ProductId = product.ProductId,
							ImageProduct = await ConvertImageToByteArray(image)
						};

						_context.ProductImages.Add(productImage);
					}
				}

				await _context.SaveChangesAsync();
				return Ok(new { message = "Продукт успешно добавлен", product.ProductId });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при добавлении продукта.", error = ex.Message });
			}
		}
		private async Task<byte[]> ConvertImageToByteArray(IFormFile image)
		{
			using (var memoryStream = new MemoryStream())
			{
				await image.CopyToAsync(memoryStream);
				return memoryStream.ToArray();
			}
		}
		public class ProductFormModel
		{
			public string Name { get; set; }
			public string Description { get; set; }
			public decimal Price { get; set; }
			public int Stock { get; set; }
			public int CategoryId { get; set; }
			public string Supplier { get; set; }
			public string CountryOfOrigin { get; set; }
			public DateTime ExpirationDate { get; set; }
		}

		[HttpGet("GetProductImages/{productId}")]
		public IActionResult GetProductImages(int productId)
		{
			try
			{
				var productImages = _context.ProductImages
					.Where(pi => pi.ProductId == productId)
					.Select(pi => pi.ImageProduct)
					.ToList();

				if (productImages == null || !productImages.Any())
				{
					return NotFound(); // Возвращаем 404, если изображений нет
				}

				return Ok(productImages); // Возвращаем список массивов байтов
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("GetAllProducts")]
		public async Task<IActionResult> GetAllProducts()
		{
			try
			{
				// Загружаем продукты вместе с их изображениями
				var products = await _context.Products
					.Include(p => p.ProductImages) // Подгружаем связанные изображения
					.ToListAsync();

				// Формируем DTO для вывода
				var result = products.Select(p => new
				{
					p.ProductId,
					p.Name,
					p.Description,
					p.Price,
					p.Stock,
					p.CategoryId,
					p.Supplier,
					p.CountryOfOrigin,
					p.ExpirationDate,
					p.IsDeleted,
					Images = p.ProductImages.Select(img => Convert.ToBase64String(img.ImageProduct)).ToList() // Преобразуем изображения в Base64
				}).ToList();

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("GetProductImage/{productId}")]
		public IActionResult GetProductImage(int productId)
		{
			try
			{
				var productImage = _context.ProductImages
					.FirstOrDefault(pi => pi.ProductId == productId);

				if (productImage == null || productImage.ImageProduct == null)
				{
					return NotFound(); // Возвращаем 404, если изображение не найдено
				}

				return File(productImage.ImageProduct, "image/jpeg"); // Возвращаем изображение
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("SearchProduct")]
		public async Task<IActionResult> SearchProduct(string query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return BadRequest("Запрос не должен быть пустым.");

			// Убираем лишние пробелы и приводим к нижнему регистру для сравнения
			var normalizedQuery = query.Trim().ToLower();

			// Сначала ищем товары, название которых начинается с запроса
			var products = await _context.Products
				.Where(p => p.Name.ToLower().StartsWith(normalizedQuery) ||
							p.ProductId.ToString() == normalizedQuery)
				.ToListAsync();

			return products.Any() ? Ok(products) : NotFound("Продукты не найдены.");
		}
		[HttpGet("GetProductById")]
		public async Task<IActionResult> GetProductById(int id)
		{
			// Проверяем, что переданный Id больше нуля (или корректен для вашего случая)
			if (id <= 0)
				return BadRequest("Идентификатор должен быть положительным числом.");

			// Ищем продукт по Id
			var product = await _context.Products
				.FirstOrDefaultAsync(p => p.ProductId == id);

			// Если продукт найден, возвращаем его
			if (product != null)
				return Ok(product);

			// Если продукт не найден, возвращаем NotFound
			return NotFound("Продукт с указанным Id не найден.");
		}

		[HttpGet("GetImagesByProductId/{productId}")]
		public async Task<IActionResult> GetImagesByProductId(int productId)
		{
			var images = await _context.ProductImages
				.Where(img => img.ProductId == productId)
				.Select(img => img.ImageProduct)
				.ToListAsync();

			return Ok(images);
		}

		[HttpPut("ChangeProduct")]
		public async Task<IActionResult> ChangeProduct([FromBody] Product productDto)
		{
			var existingProduct = await _context.Products.FindAsync(productDto.ProductId);
			if (existingProduct == null)
				return NotFound("Продукт не найден.");

			existingProduct.Name = productDto.Name;
			existingProduct.Description = productDto.Description;
			existingProduct.Price = productDto.Price;
			existingProduct.Stock = productDto.Stock;
			existingProduct.CategoryId = productDto.CategoryId;
			existingProduct.Supplier = productDto.Supplier;
			existingProduct.CountryOfOrigin = productDto.CountryOfOrigin;

			await _context.SaveChangesAsync();
			return Ok("Продукт успешно обновлён.");
		}

		[HttpPut("RestoreProduct/{id}")]
		public async Task<IActionResult> RestoreProduct(int id)
		{
			var product = await _context.Products.FindAsync(id);
			if (product == null)
				return NotFound("Товар не найден.");

			product.IsDeleted = false;
			await _context.SaveChangesAsync();

			return Ok("Товар восстановлен.");
		}
		[HttpPost("AddImage")]
		public async Task<IActionResult> AddImage([FromQuery] int productId, IFormFile image)
		{
			if (image == null || image.Length == 0)
				return BadRequest("Файл не выбран или пуст.");

			using var ms = new MemoryStream();
			await image.CopyToAsync(ms);
			byte[] imageData = ms.ToArray();

			var newImage = new ProductImage
			{
				ProductId = productId,
				ImageProduct = imageData
			};

			_context.ProductImages.Add(newImage);
			await _context.SaveChangesAsync();

			return Ok("Изображение добавлено.");
		}
		[HttpPut("DeleteProduct/{id}")]
		public async Task<IActionResult> DeleteProduct(int id)
		{
			var product = await _context.Products.FindAsync(id);
			if (product == null)
				return NotFound("Продукт не найден.");

			product.IsDeleted = true;
			await _context.SaveChangesAsync();
			return Ok("Продукт помечен как удалён.");
		}
		[HttpDelete("DeleteImages")]
		public async Task<IActionResult> DeleteImages([FromQuery] int productId)
		{
			var images = _context.ProductImages.Where(img => img.ProductId == productId);
			if (!images.Any())
				return NotFound("Изображения не найдены.");

			_context.ProductImages.RemoveRange(images);
			await _context.SaveChangesAsync();

			return Ok("Изображения удалены.");
		}


		[HttpGet("GetProductStock")]
		public IActionResult GetProductStock(int productId)
		{
			if (productId <= 0)
				return BadRequest(new { message = "Некорректный ID товара." });

			var product = _context.Products.FirstOrDefault(p => p.ProductId == productId);
			if (product == null)
				return NotFound(new { message = "Товар не найден." });

			return Ok(product.Stock);
		}
		[HttpPost("AddSimpleProduct")]
		public async Task<IActionResult> AddSimpleProduct([FromForm] SimpleProductModel model, IFormFile Image)
		{
			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.ToList();

				return BadRequest(new { message = "Некорректные данные", errors });
			}

			try
			{
				var product = new Product
				{
					Name = model.Name,
					Price = model.Price,
					Stock = model.Stock,
					Description = model.Description ?? "",
					Supplier = model.Supplier ?? "",
					CountryOfOrigin = model.CountryOfOrigin ?? "",
					ExpirationDate = model.ExpirationDate ?? "",
					CategoryId = 1
				};

				_context.Products.Add(product);
				await _context.SaveChangesAsync();

				// Сохраняем изображение в таблицу ProductImages
				if (Image != null && Image.Length > 0)
				{
					using var memoryStream = new MemoryStream();
					await Image.CopyToAsync(memoryStream);
					byte[] imageData = memoryStream.ToArray();

					var productImage = new ProductImage
					{
						ProductId = product.ProductId,
						ImageProduct = imageData
					};

					_context.ProductImages.Add(productImage);
					await _context.SaveChangesAsync();
				}

				return Ok(new { message = "Товар успешно добавлен" });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при добавлении товара", error = ex.Message });
			}
		}


	}
}
public class SimpleProductModel
{
	public string Name { get; set; }
	public string Description { get; set; }
	public decimal Price { get; set; }
	public int Stock { get; set; }
	public string Supplier { get; set; }
	public string CountryOfOrigin { get; set; }  
	public string ExpirationDate { get; set; }
}