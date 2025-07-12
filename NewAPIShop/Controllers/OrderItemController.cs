using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;
using System.Text;

namespace NewAPIShop.Controllers
{
	public class OrderItemController : Controller
	{
		private readonly ProductshopwmContext _context;

		public OrderItemController(ProductshopwmContext context)
		{
			_context = context;
		}
		[HttpGet("{GetOrderItemsByOrderId}")]
		public async Task<IActionResult> GetOrderItemsByOrderId(int orderId)
		{
			if (orderId <= 0)
				return BadRequest("Некорректный ID заказа.");

			var items = await _context.OrderItems
				.Where(oi => oi.OrderId == orderId)
				.Include(oi => oi.Product)
				.ToListAsync();

			if (!items.Any())
				return NotFound("Элементы заказа не найдены.");

			return Ok(items);
		}
		[HttpPost("AddOrderItem")]
		public async Task<IActionResult> AddOrderItem(int orderId, int productId, int quantity)
		{
			if (orderId <= 0 || productId <= 0 || quantity <= 0)
				return BadRequest("Некорректные данные.");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				var order = await _context.Orders.FindAsync(orderId);
				if (order == null)
					return NotFound("Заказ не найден.");

				var product = await _context.Products.FindAsync(productId);
				if (product == null)
					return NotFound("Товар не найден.");

				if (product.Stock < quantity)
					return BadRequest("Недостаточно товара на складе.");

				var existingItem = await _context.OrderItems
					.FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.ProductId == productId);

				if (existingItem != null)
				{
					existingItem.Quantity += quantity;
					existingItem.Price = product.Price;
					_context.OrderItems.Update(existingItem);
				}
				else
				{
					var newItem = new OrderItem
					{
						OrderId = orderId,
						ProductId = productId,
						Quantity = quantity,
						Price = product.Price
					};
					await _context.OrderItems.AddAsync(newItem);
				}

				await _context.SaveChangesAsync();

				product.Stock -= quantity;
				_context.Products.Update(product);
				await _context.SaveChangesAsync();

				order.Total = await _context.OrderItems
					.Where(oi => oi.OrderId == orderId)
					.SumAsync(oi => oi.Quantity * oi.Price);

				_context.Orders.Update(order);
				await _context.SaveChangesAsync();

				await transaction.CommitAsync();

				return Ok(new
				{
					Message = "Товар добавлен в заказ.",
					OrderId = orderId
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return StatusCode(500, new { Message = "Ошибка при добавлении товара в заказ.", Error = ex.Message });
			}
		}
		[HttpPut("UpdateOrderItemQuantity")]
		public async Task<IActionResult> UpdateOrderItemQuantity(int orderItemId, int newQuantity)
		{
			if (orderItemId <= 0 || newQuantity <= 0)
				return BadRequest("Некорректные данные.");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				var item = await _context.OrderItems
					.Include(oi => oi.Product)
					.FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);

				if (item == null)
					return NotFound("Элемент заказа не найден.");

				var product = item.Product;
				int difference = newQuantity - item.Quantity;

				if (product.Stock < difference)
					return BadRequest("Недостаточно товара на складе.");

				item.Quantity = newQuantity;
				_context.OrderItems.Update(item);
				await _context.SaveChangesAsync();

				product.Stock -= difference;
				_context.Products.Update(product);
				await _context.SaveChangesAsync();

				var order = await _context.Orders.FindAsync(item.OrderId);
				order.Total = await _context.OrderItems
					.Where(oi => oi.OrderId == item.OrderId)
					.SumAsync(oi => oi.Quantity * oi.Price);

				_context.Orders.Update(order);
				await _context.SaveChangesAsync();

				await transaction.CommitAsync();

				return Ok(new
				{
					Message = "Количество обновлено.",
					item
				});
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return StatusCode(500, new { Message = "Ошибка при обновлении количества.", Error = ex.Message });
			}
		}
		[HttpDelete("DeleteOrderItem")]
		public async Task<IActionResult> DeleteOrderItem(int orderItemId)
		{
			if (orderItemId <= 0)
				return BadRequest("Некорректный ID элемента заказа.");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				var item = await _context.OrderItems
					.Include(oi => oi.Product)
					.FirstOrDefaultAsync(oi => oi.OrderItemId == orderItemId);

				if (item == null)
					return NotFound("Элемент заказа не найден.");

				// Вернуть товар обратно
				item.Product.Stock += item.Quantity;
				_context.Products.Update(item.Product);

				// Удалить элемент
				_context.OrderItems.Remove(item);
				await _context.SaveChangesAsync();

				// Пересчитать сумму заказа
				var order = await _context.Orders.FindAsync(item.OrderId);
				order.Total = await _context.OrderItems
					.Where(oi => oi.OrderId == item.OrderId)
					.SumAsync(oi => oi.Quantity * oi.Price);

				_context.Orders.Update(order);
				await _context.SaveChangesAsync();

				await transaction.CommitAsync();

				return Ok(new { Message = "Элемент удален из заказа." });
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return StatusCode(500, new { Message = "Ошибка при удалении элемента заказа.", Error = ex.Message });
			}
		}
		[HttpPost("ConfirmDeliveryy")]
		public async Task<IActionResult> ConfirmDeliveryy(int orderId)
		{
			if (orderId <= 0)
			{
				return Content("<h1>Ошибка</h1><p>Неверный идентификатор заказа.</p>", "text/html", Encoding.UTF8);
			}

			var order = await _context.Orders.FindAsync(orderId);
			if (order == null)
			{
				return Content($"<h1>❌ Ошибка</h1><p>Заказ с ID <strong>{orderId}</strong> не найден.</p>", "text/html", Encoding.UTF8);
			}

			if (order.PaymentStatus == "Выдан")
			{
				string issuedDate = order.PickupDate?.ToString("dd.MM.yyyy HH:mm") ?? "неизвестно";
				return Content($@"
			<html>
				<head><title>Заказ уже выдан</title></head>
				<body style='font-family: Arial; text-align: center; margin-top: 100px;'>
					<h1>⚠️ Заказ уже выдан</h1>
					<p>Заказ №<strong>{orderId}</strong> был ранее выдан.</p>
					<p>Дата выдачи: <strong>{issuedDate}</strong></p>
				</body>
			</html>", "text/html", Encoding.UTF8);
			}

			order.PaymentStatus = "Выдан";
			order.PickupDate = DateTime.Now;

			_context.Orders.Update(order);
			await _context.SaveChangesAsync();

			string formattedDate = order.PickupDate?.ToString("dd.MM.yyyy HH:mm") ?? "";

			string htmlResponse = $@"
	<html>
		<head>
			<title>Выдача заказа</title>
		</head>
		<body style='font-family: Arial; text-align: center; margin-top: 100px;'>
			<h1>✅ Заказ №{orderId} выдан</h1>
			<p>Дата выдачи: <strong>{formattedDate}</strong></p>
			<p>Спасибо за покупку!</p>
		</body>
	</html>";

			return Content(htmlResponse, "text/html", Encoding.UTF8);
		}

		[HttpPost("CancelOrder")]
		public IActionResult CancelOrder(int orderId)
		{
			var order = _context.Orders.FirstOrDefault(o => o.OrderId == orderId);
			if (order == null)
				return NotFound("Заказ не найден.");

			if (order.PaymentStatus == "Выдан")
				return BadRequest("Нельзя отменить уже выданный заказ.");

			order.PaymentStatus = "Отменён";
			_context.SaveChanges();

			return Ok();
		}
		[HttpGet("GetOrderById")]
		public async Task<IActionResult> GetOrderById(int id)
		{
			var order = await _context.Orders
			 .Include(o => o.OrderItems)
			  .ThenInclude(oi => oi.Product)
			 .FirstOrDefaultAsync(o => o.OrderId == id);

			if (order == null)
				return NotFound();

			var dto = new OrderDtooo
			{
				OrderId = order.OrderId,
				OrderDate = order.OrderDate,
				PaymentStatus = order.PaymentStatus,
				PickupDate = order.PickupDate,
				Total = order.Total,
				IsPickup = order.IsPickup,
				OrderItems = order.OrderItems.Select(oi => new OrderItemDtooo
				{
					OrderItemId = oi.OrderItemId,
					ProductName = oi.Product.Name,
					Price = oi.Price,
					Quantity = oi.Quantity
				}).ToList()
			};

			return Ok(dto);
		}
		[HttpGet("ConfirmDeliveryy")]
		public async Task<IActionResult> ConfirmDeliveryyFromQr([FromQuery] int orderId)
		{
			if (orderId <= 0)
			{
				return Content("<h1>Ошибка</h1><p>Неверный ID заказа.</p>", "text/html", Encoding.UTF8);
			}

			var order = await _context.Orders.FindAsync(orderId);
			if (order == null)
			{
				return Content($"<h1>❌ Ошибка</h1><p>Заказ #{orderId} не найден.</p>", "text/html", Encoding.UTF8);
			}

			if (order.PaymentStatus == "Выдан")
			{
				string issuedDate = order.PickupDate?.ToString("dd.MM.yyyy HH:mm") ?? "неизвестно";
				return Content($@"
   <html>
    <head><title>Заказ уже выдан</title></head>
    <body style='font-family: Arial; text-align: center; margin-top: 100px;'>
     <h1>⚠️ Заказ уже выдан</h1>
     <p>Заказ #{orderId} был ранее выдан.</p>
     <p>Дата выдачи: <strong>{issuedDate}</strong></p>
    </body>
   </html>", "text/html", Encoding.UTF8);
			}

			order.PaymentStatus = "Выдан";
			order.PickupDate = DateTime.Now;
			await _context.SaveChangesAsync();

			return Content($@"
  <html>
   <head><title>Заказ выдан</title></head>
   <body style='font-family: Arial; text-align: center; margin-top: 100px;'>
    <h1>✅ Заказ #{orderId} выдан</h1>
    <p>Дата выдачи: <strong>{order.PickupDate:dd.MM.yyyy HH:mm}</strong></p>
    <p>Спасибо за покупку!</p>
   </body>
  </html>", "text/html", Encoding.UTF8);
		}
	}
}
public class OrderDtooo
{
	public int OrderId { get; set; }
	public DateTime OrderDate { get; set; }
	public string PaymentStatus { get; set; }
	public DateTime? PickupDate { get; set; }
	public decimal Total { get; set; }
	public bool IsPickup { get; set; }
	public List<OrderItemDtooo> OrderItems { get; set; }
}
public class OrderItemDtooo
{
	public int OrderItemId { get; set; }
	public string ProductName { get; set; }
	public decimal Price { get; set; }
	public int Quantity { get; set; }
}