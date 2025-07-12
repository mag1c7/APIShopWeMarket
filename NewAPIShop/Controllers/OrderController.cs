using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class OrderController : Controller
	{
		private readonly ProductshopwmContext _context;

		public OrderController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpGet("GetOrdersByUserId")]
		public async Task<IActionResult> GetOrdersByUserId(int userId)
		{
			if (userId <= 0)
				return BadRequest("Некорректный ID пользователя.");

			var orders = await _context.Orders
				.Where(o => o.UserId == userId)
				.Include(o => o.OrderItems)
					.ThenInclude(oi => oi.Product)
				.Select(order => new OrderDto
				{
					OrderId = order.OrderId,
					OrderDate = order.OrderDate,
					PaymentStatus = order.PaymentStatus,
					IsPickup = order.IsPickup,
					Total = order.Total,
					OrderItems = order.OrderItems.Select(oi => new OrderItemDto
					{
						OrderItemId = oi.OrderItemId,
						ProductId = oi.ProductId,
						ProductName = oi.Product.Name,
						Price = oi.Price,
						Quantity = oi.Quantity
					}).ToList()
				})
				.ToListAsync();

			if (!orders.Any())
				return NotFound("Заказы не найдены.");

			return Ok(orders);
		}

		[HttpPost("CreateOrder")]
		public async Task<IActionResult> CreateOrder(int userId, int productId, int quantity)
		{
			if (userId <= 0 || productId <= 0 || quantity <= 0)
				return BadRequest(new { message = "Некорректные данные." });

			var user = await _context.Users.FindAsync(userId);
			if (user == null)
				return NotFound(new { message = "Пользователь не найден." });

			var product = await _context.Products.FindAsync(productId);
			if (product == null)
				return NotFound(new { message = "Товар не найден." });

			if (product.Stock < quantity)
				return BadRequest(new { message = $"Недостаточно товара на складе. Доступно: {product.Stock} шт." });

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				// Создаем заказ
				var newOrder = new Order
				{
					UserId = userId,
					OrderDate = DateTime.UtcNow,
					PaymentStatus = "pending",
					IsPickup = true,
					Total = product.Price * quantity
				};

				await _context.Orders.AddAsync(newOrder);
				await _context.SaveChangesAsync();

				// Добавляем элементы заказа
				var orderItem = new OrderItem
				{
					OrderId = newOrder.OrderId,
					ProductId = productId,
					Quantity = quantity,
					Price = product.Price
				};

				await _context.OrderItems.AddAsync(orderItem);
				await _context.SaveChangesAsync();

				// Уменьшаем остаток товара
				product.Stock -= quantity;
				_context.Products.Update(product);
				await _context.SaveChangesAsync();

				await transaction.CommitAsync();

				return Ok(new
				{
					orderId = newOrder.OrderId,
					message = "Заказ успешно создан.",
					total = newOrder.Total
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync(); // Откатываем транзакцию
				return StatusCode(500, new { message = "Ошибка при создании заказа.", error = ex.Message });
			}
		}
		[HttpGet("GetAllOrders")]
		public async Task<IActionResult> GetAllOrders()
		{
			var orders = await _context.Orders
				.Include(o => o.PickupPoint)
				.Include(o => o.OrderItems)
					.ThenInclude(oi => oi.Product)
				.Include(o => o.User)
				.Select(order => new OrderDtoo
				{
					OrderId = order.OrderId,
					OrderDate = order.OrderDate,
					PaymentStatus = order.PaymentStatus,
					IsPickup = order.IsPickup,
					Total = order.Total,
					PickupPoint = order.PickupPoint != null ? new PickupPointDto
					{
						PickupPointId = order.PickupPoint.PickupPointId,
						Address = order.PickupPoint.Address,
						Description = order.PickupPoint.Description
					} : null,
					OrderItems = order.OrderItems.Select(oi => new OrderItemDto
					{
						OrderItemId = oi.OrderItemId,
						ProductId = oi.ProductId,
						ProductName = oi.Product.Name,
						Price = oi.Price,
						Quantity = oi.Quantity
					}).ToList()
				})
				.ToListAsync();

			return Ok(orders);
		}
		[HttpGet("GetOrderDetails")]
		public async Task<IActionResult> GetOrderDetails(int orderId)
		{
			if (orderId <= 0)
				return BadRequest("Некорректный ID заказа.");

			var order = await _context.Orders
				.Where(o => o.OrderId == orderId)
				.Include(o => o.User)
				.Include(o => o.OrderItems)
					.ThenInclude(oi => oi.Product)
				.FirstOrDefaultAsync();

			if (order == null)
				return NotFound("Заказ не найден.");

			return Ok(order);
		}
		[HttpGet("GetOrderDetailss")]
		public async Task<IActionResult> GetOrderDetailss(int orderId)
		{
			if (orderId <= 0)
				return BadRequest("Некорректный ID заказа.");

			var order = await _context.Orders
				.Where(o => o.OrderId == orderId)
				.Include(o => o.User)
				.Include(o => o.OrderItems)
					.ThenInclude(oi => oi.Product)
				.FirstOrDefaultAsync();

			if (order == null)
				return NotFound("Заказ не найден.");

			// Формируем DTO
			var orderDto = new OrderDto
			{
				OrderId = order.OrderId,
				OrderDate = order.OrderDate,
				PaymentStatus = order.PaymentStatus,
				IsPickup = order.IsPickup,
				Total = order.Total,
				OrderItems = order.OrderItems.Select(oi => new OrderItemDto
				{
					OrderItemId = oi.OrderItemId,
					ProductId = oi.ProductId,
					ProductName = oi.Product?.Name ?? "Неизвестный товар",
					Price = oi.Price,
					Quantity = oi.Quantity
				}).ToList(),
				//UserName = $"{order.User?.Name} {order.User?.Surname}".Trim()
			};

			return Ok(orderDto);
		}

		[HttpGet("GetOrdersByUserIdWithPickup")]
		public async Task<IActionResult> GetOrdersByUserIdWithPickup(int userId)
		{
			if (userId <= 0)
				return BadRequest("Некорректный ID пользователя.");

			var orders = await _context.Orders
				.Where(o => o.UserId == userId)
				.Include(o => o.PickupPoint)
				.Include(o => o.OrderItems)
					.ThenInclude(i => i.Product)
				.Select(order => new OrderDtoo
				{
					OrderId = order.OrderId,
					OrderDate = order.OrderDate,
					PaymentStatus = order.PaymentStatus,
					IsPickup = order.IsPickup,
					Total = order.Total,
					PickupPoint = order.PickupPoint != null ? new PickupPointDto
					{
						PickupPointId = order.PickupPoint.PickupPointId,
						Address = order.PickupPoint.Address,
						Description = order.PickupPoint.Description
					} : null,
					OrderItems = order.OrderItems.Select(oi => new OrderItemDto
					{
						OrderItemId = oi.OrderItemId,
						ProductId = oi.ProductId,
						ProductName = oi.Product.Name,
						Price = oi.Price,
						Quantity = oi.Quantity
					}).ToList()
				})
				.ToListAsync();

			if (!orders.Any())
				return NotFound("Заказов не найдено");

			return Ok(orders);
		}
		[HttpGet("GetOrdersByStatus")]
		public async Task<IActionResult> GetOrdersByStatus(string status)
		{
			if (string.IsNullOrWhiteSpace(status))
				return BadRequest("Не указан статус");

			var validStatuses = new[] { "В процессе", "Выдан", "Отменён" };
			if (!validStatuses.Contains(status))
				return BadRequest("Неверный статус");

			var orders = await _context.Orders
				.Where(o => o.PaymentStatus == status)
				.Include(o => o.PickupPoint)
				.Include(o => o.OrderItems)
					.ThenInclude(i => i.Product)
				.Select(order => new OrderDtoo
				{
					OrderId = order.OrderId,
					OrderDate = order.OrderDate,
					PaymentStatus = order.PaymentStatus,
					IsPickup = order.IsPickup,
					Total = order.Total,
					PickupPoint = order.PickupPoint != null ? new PickupPointDto
					{
						PickupPointId = order.PickupPoint.PickupPointId,
						Address = order.PickupPoint.Address,
						Description = order.PickupPoint.Description
					} : null,
					OrderItems = order.OrderItems.Select(oi => new OrderItemDto
					{
						OrderItemId = oi.OrderItemId,
						ProductId = oi.ProductId,
						ProductName = oi.Product.Name,
						Price = oi.Price,
						Quantity = oi.Quantity
					}).ToList()
				})
				.ToListAsync();

			return Ok(orders);
		}
		[HttpPut("UpdatePickupDate")]
		public async Task<IActionResult> UpdatePickupDate(int orderId, [FromBody] DateOnly pickupDate)
		{
			var order = await _context.Orders.FindAsync(orderId);
			if (order == null)
				return NotFound("Заказ не найден");

			order.PickupDate = pickupDate.ToDateTime(TimeOnly.MinValue); // или используй DateTime

			await _context.SaveChangesAsync();

			return Ok(new { message = "Дата выдачи обновлена", pickupDate });
		}
	}
}
public class OrderItemDto
{
	public int OrderItemId { get; set; }
	public int ProductId { get; set; }
	public string ProductName { get; set; }
	public decimal Price { get; set; }
	public int Quantity { get; set; }
}

public class OrderDto
{
	public int OrderId { get; set; }
	public DateTime OrderDate { get; set; }
	public string PaymentStatus { get; set; }
	public bool IsPickup { get; set; }
	public decimal Total { get; set; }

	public List<OrderItemDto> OrderItems { get; set; } = new();
}
public class PickupPointDto
{
	public int PickupPointId { get; set; }
	public string Address { get; set; }
	public string Description { get; set; }
}
public class OrderDtoo
{
	public int OrderId { get; set; }
	public DateTime OrderDate { get; set; }
	public string PaymentStatus { get; set; }
	public bool IsPickup { get; set; }
	public decimal Total { get; set; }
	public PickupPointDto? PickupPoint { get; set; }

	public List<OrderItemDto> OrderItems { get; set; } = new();
}