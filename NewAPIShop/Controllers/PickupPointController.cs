using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewAPIShop.DataBase;

namespace NewAPIShop.Controllers
{
	public class PickupPointController : Controller
	{
		private readonly ProductshopwmContext _context;

		public PickupPointController(ProductshopwmContext context)
		{
			_context = context;
		}

		[HttpGet("GetAllPickupPoints")]
		public async Task<IActionResult> GetAllPickupPoints()
		{
			var points = await _context.PickupPoints.ToListAsync();
			return Ok(points);
		}

	}
}
