using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class CartController : ControllerBase
	{
		private readonly ProductshopwmContext _context;

		public CartController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpPost("AddToCart")]
		public async Task<IActionResult> AddToCart(int userId, int productId, int quantity)
		{
			quantity = 1;

			var userAndProduct = await _context.Users
				.Where(u => u.UserId == userId)
				.Select(u => new { User = u, Product = _context.Products.FirstOrDefault(p => p.ProductId == productId) })
				.FirstOrDefaultAsync();

			if (userAndProduct?.User == null)
			{
				return NotFound(new { message = "Пользователь не найден." });
			}

			var product = userAndProduct.Product;
			if (product == null)
			{
				return NotFound(new { message = $"Товар с ID {productId} не найден." });
			}

			if (product.Stock == 0)
			{
				return BadRequest(new { message = $"Товар '{product.Name}' недоступен для покупки, так как его нет в наличии." });
			}

			if (product.Stock < quantity)
			{
				return BadRequest(new { message = $"Недостаточно товара на складе. Доступно: {product.Stock}" });
			}

			using (var transaction = await _context.Database.BeginTransactionAsync())
			{
				try
				{
					var cartItem = await _context.Carts
						.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

					if (cartItem != null)
					{
						cartItem.Quantity += quantity;
					}
					else
					{
						_context.Carts.Add(new Cart
						{
							UserId = userId,
							ProductId = productId,
							Quantity = quantity
						});
					}

					await _context.SaveChangesAsync();
					await transaction.CommitAsync();
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					//_logger.LogError(ex, "Ошибка при добавлении товара в корзину.");
					return StatusCode(500, new { message = "Ошибка сервера." });
				}
			}

			return Ok(new
			{
				message = "Товар успешно добавлен в корзину.",
				cartItemCount = await _context.Carts.CountAsync(c => c.UserId == userId)
			});
		}

		[HttpPut("UpdateCartItem")]
		public async Task<IActionResult> UpdateCartItem(int userId, int productId, int quantity)
		{
			if (quantity <= 0)
			{
				return BadRequest(new { message = "Количество должно быть больше нуля." });
			}

			var userAndProduct = await _context.Users
				.Where(u => u.UserId == userId)
				.Select(u => new { User = u, Product = _context.Products.FirstOrDefault(p => p.ProductId == productId) })
				.FirstOrDefaultAsync();

			if (userAndProduct?.User == null)
			{
				return NotFound(new { message = "Пользователь не найден." });
			}

			var product = userAndProduct.Product;
			if (product == null)
			{
				return NotFound(new { message = $"Товар с ID {productId} не найден." });
			}

			if (product.Stock == 0)
			{
				return BadRequest(new { message = $"Товар '{product.Name}' отсутствует на складе и не может быть обновлен." });
			}

			if (product.Stock < quantity)
			{
				return BadRequest(new { message = $"Недостаточно товара на складе. Доступно: {product.Stock}" });
			}

			using (var transaction = await _context.Database.BeginTransactionAsync())
			{
				try
				{
					var cartItem = await _context.Carts
						.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

					if (cartItem == null)
					{
						return NotFound(new { message = "Товар не найден в корзине." });
					}

					cartItem.Quantity = quantity;

					product.Stock -= quantity - cartItem.Quantity;

					await _context.SaveChangesAsync();
					await transaction.CommitAsync();
				}
				catch (Exception ex)
				{
					await transaction.RollbackAsync();
					//_logger.LogError(ex, "Ошибка при обновлении товара в корзине.");
					return StatusCode(500, new { message = "Ошибка сервера." });
				}
			}

			return Ok(new
			{
				message = "Количество товара успешно обновлено.",
				cartItemCount = await _context.Carts.CountAsync(c => c.UserId == userId),
				updatedQuantity = quantity
			});
		}
		[HttpGet("CheckCartItem")]
		public IActionResult CheckCartItem(int userId, int productId)
		{
			try
			{
				var cartItem = _context.Carts.FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);

				bool exists = cartItem != null;
				return Ok(exists);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("TakeInfoCartItem")]
		public IActionResult TakeInfoCartItem(int userId, int productId)
		{
			try
			{
				var cartItem = _context.Carts.FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);


				return Ok(cartItem);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("GetCartItems")]
		public IActionResult GetCartItems(int userId)
		{
			try
			{
				var cartItems = _context.Carts
					.Where(c => c.UserId == userId)
					.Select(c => new
					{
						ProductId = c.ProductId,
						ProductName = c.Product.Name,
						Quantity = c.Quantity,
						Price = c.Product.Price,

						Image = c.Product.ProductImages
							.OrderBy(pi => pi.ImageId)
							.Select(pi => pi.ImageProduct)
							.FirstOrDefault()
					})
					.ToList();

				int totalQuantity = cartItems.Sum(item => item.Quantity);

				return Ok(new { totalQuantity, cartItems });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}
		[HttpGet("GetCartItemsBuy")]
		public IActionResult GetCartItemsBuy(int userId)
		{
			try
			{
				var cartItems = _context.Carts
					.Where(c => c.UserId == userId)
					.Select(c => new
					{
						ProductId = c.ProductId,
						ProductName = c.Product.Name,
						Quantity = c.Quantity,
						Price = c.Product.Price
					})
					.ToList();

				int totalQuantity = cartItems.Sum(item => item.Quantity);

				return Ok(new { totalQuantity, cartItems });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
			}
		}

		[HttpGet("GetUserCartProducts")]
		public async Task<IActionResult> GetUserCartProducts(int userId)
		{
			if (userId <= 0)
				return BadRequest("Некорректный ID пользователя.");

			var cartProducts = await _context.Carts
				.Where(c => c.UserId == userId)
				.Select(c => new
				{
					c.Product.ProductId,
					c.Product.Name,
					c.Product.Description,
					c.Product.Price,
					c.Product.Stock,
					c.Product.CategoryId,
					c.Product.Supplier,
					c.Product.CountryOfOrigin,
					c.Product.ExpirationDate,
					//c.Product.Image
				})
				.ToListAsync();

			return cartProducts.Any() ? Ok(cartProducts) : NotFound("Корзина пользователя пуста.");
		}

		[HttpDelete("DeleteFromCart")]
		public async Task<IActionResult> DeleteFromCart(int userId, int productId)
		{
			try
			{
				if (!_context.Users.Any(u => u.UserId == userId))
				{
					return NotFound(new { message = "Пользователь не найден." });
				}

				if (!_context.Products.Any(p => p.ProductId == productId))
				{
					return NotFound(new { message = "Товар не найден." });
				}

				var cartItem = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
				if (cartItem == null)
				{
					return NotFound(new { message = "Товар не найден в корзине." });
				}

				_context.Carts.Remove(cartItem);
				await _context.SaveChangesAsync();

				return Ok(new { message = "Товар успешно удален из корзины." });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка сервера.", error = ex.Message });
			}
		}

		[HttpGet("GetUserById")]
		public async Task<IActionResult> GetUserById(int userId)
		{
			if (userId <= 0)
				return BadRequest(new { message = "Некорректный ID пользователя." });

			var user = await _context.Users.FindAsync(userId);
			if (user == null)
				return NotFound(new { message = "Пользователь не найден." });

			return Ok(user);
		}
		[HttpGet("GetProduct")]
		public async Task<IActionResult> GetProduct(int productId)
		{
			if (productId <= 0)
				return BadRequest(new { message = "Некорректный ID товара." });

			var product = await _context.Products
				.Include(p => p.ProductImages)
				.FirstOrDefaultAsync(p => p.ProductId == productId);

			if (product == null)
				return NotFound(new { message = "Товар не найден." });

			return Ok(product);
		}
		[HttpGet("GetProductBuy")]
		public async Task<IActionResult> GetProductBuy(int productId)
		{
			if (productId <= 0)
				return BadRequest(new { message = "Некорректный ID товара." });

			var product = await _context.Products
				.Where(p => p.ProductId == productId)
				.Select(p => new
				{
					p.ProductId,
					p.Name,
					p.Description,
					p.Price,
					p.Stock,
					p.Supplier,
					p.CountryOfOrigin,
					p.ExpirationDate,
					p.CategoryId
				})
				.FirstOrDefaultAsync();

			if (product == null)
				return NotFound(new { message = "Товар не найден." });

			return Ok(product);
		}
		[HttpDelete("ClearCart")]
		public async Task<IActionResult> ClearCart(int userId)
		{
			if (userId <= 0)
				return BadRequest(new { message = "Некорректный ID пользователя." });

			var cartItems = await _context.Carts.Where(c => c.UserId == userId).ToListAsync();

			if (!cartItems.Any())
				return NotFound(new { message = "Корзина уже пуста." });

			_context.Carts.RemoveRange(cartItems);
			await _context.SaveChangesAsync();

			return Ok(new { message = "Корзина успешно очищена." });
		}
		[HttpPut("UpdateProductStock")]
		public async Task<IActionResult> UpdateProductStock(int productId, int newStock)
		{
			if (productId <= 0 || newStock < 0)
				return BadRequest(new { message = "Некорректные данные." });

			var product = await _context.Products.FindAsync(productId);
			if (product == null)
				return NotFound(new { message = "Товар не найден." });

			product.Stock = newStock;
			_context.Products.Update(product);
			await _context.SaveChangesAsync();

			return Ok(new { message = $"Остаток товара обновлен: {newStock}" });
		}
		[HttpPost("CreateOrderFromCart")]
		public async Task<IActionResult> CreateOrderFromCart([FromBody] CreateOrderRequest request)
		{
			if (request == null)
				return BadRequest(new { message = "Некорректные данные." });

			int userId = request.UserId;
			int pickupPointId = request.PickupPointId;

			if (userId <= 0)
				return BadRequest(new { message = "Некорректный ID пользователя." });

			if (pickupPointId <= 0)
				return BadRequest(new { message = "Выберите пункт выдачи." });

			var pickupPoint = await _context.PickupPoints.FindAsync(pickupPointId);
			if (pickupPoint == null)
				return BadRequest(new { message = "Пункт выдачи не найден." });

			try
			{
				var cartItems = await _context.Carts
					.Where(c => c.UserId == userId)
					.Include(c => c.Product)
					.ToListAsync();

				if (!cartItems.Any())
					return BadRequest(new { message = "Корзина пуста." });

				using var transaction = await _context.Database.BeginTransactionAsync();

				var order = new Order
				{
					UserId = userId,
					OrderDate = DateTime.UtcNow,
					PaymentStatus = "pending",
					IsPickup = true,
					Total = cartItems.Sum(i => i.Quantity * i.Product.Price),
					PickupPointId = pickupPointId
				};

				await _context.Orders.AddAsync(order);
				await _context.SaveChangesAsync();

				foreach (var item in cartItems)
				{
					var orderItem = new OrderItem
					{
						OrderId = order.OrderId,
						ProductId = item.ProductId,
						Quantity = item.Quantity,
						Price = item.Product.Price
					};
					await _context.OrderItems.AddAsync(orderItem);
				}

				await _context.SaveChangesAsync();

				foreach (var item in cartItems)
				{
					var product = await _context.Products.FindAsync(item.ProductId);
					if (product == null)
					{
						await transaction.RollbackAsync();
						return NotFound(new { message = $"Продукт {item.ProductId} не найден." });
					}

					product.Stock -= item.Quantity;
					_context.Products.Update(product);
				}

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return Ok(new { orderId = order.OrderId });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при создании заказа", error = ex.Message });
			}
		}
		[HttpPost("CreateOrderFromCartt")]
		public async Task<IActionResult> CreateOrderFromCartt([FromBody] CreateOrderRequest request)
		{
			if (request == null)
				return BadRequest(new { message = "Некорректные данные." });

			int userId = request.UserId;
			int pickupPointId = request.PickupPointId;

			if (userId <= 0)
				return BadRequest(new { message = "Некорректный ID пользователя." });

			if (pickupPointId <= 0)
				return BadRequest(new { message = "Выберите пункт выдачи." });

			var pickupPoint = await _context.PickupPoints.FindAsync(pickupPointId);
			if (pickupPoint == null)
				return BadRequest(new { message = "Пункт выдачи не найден." });

			try
			{
				var cartItems = await _context.Carts
					.Where(c => c.UserId == userId)
					.Include(c => c.Product)
					.ToListAsync();

				if (!cartItems.Any())
					return BadRequest(new { message = "Корзина пуста." });

				using var transaction = await _context.Database.BeginTransactionAsync();

				var order = new Order
				{
					UserId = userId,
					OrderDate = DateTime.UtcNow,
					PaymentStatus = "В процессе",
					IsPickup = true,
					Total = cartItems.Sum(i => i.Quantity * i.Product.Price),
					PickupPointId = pickupPointId
				};

				await _context.Orders.AddAsync(order);
				await _context.SaveChangesAsync();

				foreach (var item in cartItems)
				{
					var orderItem = new OrderItem
					{
						OrderId = order.OrderId,
						ProductId = item.ProductId,
						Quantity = item.Quantity,
						Price = item.Product.Price
					};
					await _context.OrderItems.AddAsync(orderItem);
				}

				await _context.SaveChangesAsync();

				foreach (var item in cartItems)
				{
					var product = await _context.Products.FindAsync(item.ProductId);
					if (product == null)
					{
						await transaction.RollbackAsync();
						return NotFound(new { message = $"Продукт {item.ProductId} не найден." });
					}

					if (product.Stock < item.Quantity)
					{
						await transaction.RollbackAsync();
						return BadRequest(new { message = $"Недостаточно товара: {product.Name}" });
					}

					product.Stock -= item.Quantity;
					_context.Products.Update(product);
				}

				await _context.SaveChangesAsync();
				await transaction.CommitAsync();

				return Ok(new { orderId = order.OrderId });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Ошибка при создании заказа", error = ex.Message });
			}
		}
	}
}
public class CreateOrderRequest
{
	public int UserId { get; set; }
	public int PickupPointId { get; set; }
}