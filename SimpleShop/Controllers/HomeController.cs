using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimpleShop.Data;
using SimpleShop.Models;
using System.Diagnostics;
//接待員上班報到與他的裝備(建構子)
namespace SimpleShop.Controllers
{
    public class HomeController : Controller
    {
        // 接待員的兩項基本配備：
        private readonly ApplicationDbContext _context; // 裝備一：連接倉庫的對講機(資料庫)
        private readonly ILogger<HomeController> _logger; // 裝備二：一本記錄本(用來記錄錯誤或警告)

        // 老闆把裝備交給接待員
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            //ILogger 是什麼？這就像是店裡的「監視器或工作日誌」。如果網站發生錯誤，程式設計師可以透過它把錯誤訊息
            //寫進檔案，方便之後查修（雖然這段程式碼裡剛好還沒用到它，但微軟預設都會發給接待員這本筆記本）。
            _logger = logger;
            _context = context;
        }
        /*為什麼要有 async 和 await? 這叫「非同步」。想像一下，如果倉庫很大，找商品要花 3 分鐘。如果沒有
        await，接待員就會傻傻站在原地等 3 分鐘，這期間其他客人來了他都不理；有了 await，接待員可以用對
        講機吩咐倉庫找東西，在這 3 分鐘內他還可以先去招呼其他客人，等倉庫說「找到了」，他再回來把畫面
        顯示出來。這讓你的網站能同時容納更多人！*/

        // 當客人在網址列輸入你的網站首頁時，就會執行這裡
        public async Task<IActionResult> Index()
        {
            // 步驟 1: 用對講機呼叫倉庫，但下達了一個很聰明的指令：
            // "Where(p => p.StockQuantity > 0)" -> 「只要把『庫存大於 0』的商品找出來就好！」
            // ToListAsync() -> 把它們整理成一張清單。
            var products = await _context.Products.Where(p => p.StockQuantity > 0).ToListAsync();

            // 步驟 2: 把這張「有現貨的商品清單」拿給客人看（回傳給 View 變成網頁畫面）
            return View(products);
        }

        //客人指著某個商品說：「我要看這個的詳細介紹！」(Details 方法)
        // id 就是商品的專屬編號（例如：網站.com/Home/Details/5, id 就是 5)
        // int? 代表這個編號「可能是空的」（客人可能亂打網址）
        public async Task<IActionResult> Details(int? id)
        {
            // 防呆 1: 如果客人根本沒說要看幾號商品
            if (id == null)
            {
                return NotFound(); // 接待員舉牌：「我找不到您要的網頁 (404錯誤)」
            }
            // 用對講機呼叫倉庫：「幫我找 Id 剛好等於這個數字的商品，找到第一個就拿過來！」
            var product = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);

            // 防呆 2: 如果倉庫回報說「我們店裡根本沒有這編號的商品啊」
            if (product == null)
            {
                return NotFound(); // 接待員再次舉牌：「查無此商品 (404錯誤)」
            }

            // 一切正常，把這個商品的詳細資料遞給客人看
            return View(product);
        }
        // 牆上的公告欄 (Privacy 方法)
        // 這是隱私權政策頁面
        public IActionResult Privacy()
        {
            // 這裡不需要查資料庫，直接帶客人去看寫好規定的小房間 (View) 就好
            return View();
        }
        // 店裡發生意外時的「故障告示牌」(Error 方法)
        // 這上面的中括號是一道指令：
        // 「如果發生錯誤，絕對不要把這個錯誤畫面『快取(暫存)』起來！」
        // (如果不加這行，客人可能會一直看到昨天發生的舊錯誤)
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // 產生一個 ErrorViewModel (你可以想像成一張「報修單」)
            // 裡面會塞入一個 RequestId (像是報修單號碼，方便工程師之後去日誌裡查到底哪裡壞了)
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}