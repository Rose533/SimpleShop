using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Data;
using SimpleShop.Models;
using SimpleShop.Models.Enums;
using SimpleShop.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services; // <--- 這是關鍵的 using 語句
/* 前一個 OrderController 是客人的「結帳櫃檯」，那麼這個放在 Admin 資料夾底下的 OrdersController 就是「店長專屬的後台出貨中心」！

客人在前台結帳後，訂單就會跑到這裡來，讓身為老闆（或管理員）的你可以查看訂單、包裝商品，最後按下「出貨」並寄信通知客人。*/
// 後台的終極門禁與店長裝備
namespace SimpleShop.Areas.Admin.Controllers
{
    // [Area("Admin")] 告訴系統：這個檔案屬於「後台特區」，網址前面通常會有 /Admin/
    [Area("Admin")]
    // [Authorize(Roles = "Admin")] 這是一道有「人臉辨識」的超強防盜門！
    // 白話文：只有帳號被標記為 "Admin"（管理員）角色的人才能進來。
    // 如果是一般客人想輸入網址進來，警衛會直接把他踢出去！
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        // 店長出貨必備的兩大法寶：
        private readonly ApplicationDbContext _context; // 法寶一：看全店訂單的監視器（資料庫）
        private readonly IEmailSender _emailSender;     // 法寶二：幫忙寄出貨通知的郵差

        // 老闆上班報到，領取裝備
        public OrdersController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // GET: Admin/Orders 店長看總表：「今天有幾張單要出？」（Index 方法）
        // 當店長點擊「訂單管理」時執行
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

            var orders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        /*public async Task<IActionResult> Index()
        {
            // 用監視器把「全店所有的訂單」都調出來
            var orders = await _context.Orders
                                    .Include(o => o.User) // 順便查一下這張單是哪個客人買的
                                    .OrderByDescending(o => o.OrderDate) // 【貼心設計】：把最新成立的訂單排在最上面！
                                    .ToListAsync();
            // 把這張總表拿給店長看（回傳給後台的 View）
            return View(orders);
        }*/

        // GET: Admin/Orders/Details/5 店長看明細：「這張單到底買了什麼？」（Details 方法）
        // 當店長點擊某張訂單的「查看明細」時執行（id 就是訂單編號）
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound(); // 店長沒說要看幾號單

            // 把這張訂單的「祖宗十八代」都查出來
            var order = await _context.Orders
                            .Include(o => o.User)          // 第一層：查出是哪個客人
                            .Include(o => o.OrderItems)    // 第二層：查出購物車裡有哪些明細
                            .ThenInclude(oi => oi.Product) // 第三層：透過明細，查出確切的商品名稱和圖片
                            .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound(); // 查無此單

            return View(order);
        }

        // POST: Admin/Orders/ShipOrder/5 店長包裝完畢，按下「確認出貨」！（ShipOrder 方法）
        // 這是後台最核心的動作。當店長把東西裝箱、貼上黑貓宅急便的單號後，就會在網頁上按下出貨按鈕。
        // POST 代表這是一個會「修改資料」的動作
        [HttpPost]
        [ValidateAntiForgeryToken] // 防駭客偽造按鈕
        // 接收兩個參數：id（訂單編號）, trackingNumber（物流追蹤碼，例如黑貓的單號）
        public async Task<IActionResult> ShipOrder(int id, string trackingNumber)
        {
            // 先把這張單找出來
            var order = await _context.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // 【防呆機制】：只有狀態是「等待中（Pending）」或「處理中（Processing）」的單可以出貨
            // 如果這張單已經退貨，或是昨天早就出貨過了，就不憑再按一次！
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing)
            {
                // 步驟 1：修改訂單狀態
                order.Status = OrderStatus.Shipped; // 狀態改成：已出貨！
                order.ShippedDate = DateTime.UtcNow; // 記錄按下出貨按鈕的確切時間
                order.TrackingNumber = trackingNumber; // 把黑貓單號抄進資料庫裡

                // 叫資料庫存檔
                _context.Update(order);
                await _context.SaveChangesAsync();

                // 步驟 2：呼叫郵差！寄「出貨通知信」給客人
                if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
                {
                    string subject = $"您的訂單 #{order.Id} 已出貨"; // 信件主旨

                    // 組合信件內容，這裡寫得很聰明：
                    // 如果店長有填物流單號，就顯示單號；如果店長懶得填留白，就只寫「包裹正在路上」
                    string message = $"親愛的顧客，<br><br>" +
                                     $"您的訂單編號 {order.Id} 已於 {order.ShippedDate:yyyy-MM-dd HH:mm} 出貨。<br>" +
                                     $"{(string.IsNullOrWhiteSpace(trackingNumber) ? "您的包裹正在路上。" : $"您的貨運單號為：{trackingNumber}")}<br><br>" +
                                     $"感謝您的訂購！<br><br>" +
                                     $"SimpleShop 團隊";

                    await _emailSender.SendEmailAsync(order.User.Email, subject, message); // 寄出！
                }

                // 在店長的畫面貼一張綠色的成功紙條
                TempData["SuccessMessage"] = $"訂單 #{order.Id} 已標記為已出貨。";
            }
            else
            {
                // 如果店長亂按（例如對已經出貨的訂單按出貨），貼一張紅色的警告紙條
                TempData["ErrorMessage"] = $"訂單 #{order.Id} 的狀態為 {order.Status}，無法設定為已出貨。";
            }

            // 動作完成狀況更新
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}
