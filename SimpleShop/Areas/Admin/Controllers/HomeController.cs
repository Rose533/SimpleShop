using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Data;
using SimpleShop.Models.Enums;

namespace SimpleShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ProductCount = await _context.Products.CountAsync();
            ViewBag.OrderCount = await _context.Orders.CountAsync();

            ViewBag.PendingOrderCount = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Pending);

            var todayOrders = await _context.Orders
                .Where(o => o.OrderDate.Date == DateTime.UtcNow.Date)
                .ToListAsync();

            ViewBag.TodayRevenue = todayOrders.Sum(o => o.TotalAmount);

            return View();
        }
    }
}

/*using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SimpleShop.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // 只有 Admin 角色才能存取
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}*/