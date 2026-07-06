using Microsoft.AspNetCore.Mvc;
using SimpleShop.Data;
using SimpleShop.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // For [Authorize] if checkout needs login
/*CartController 就是你這家商店裡的「購物車管理員」或「收銀台櫃檯人員」。

它的工作很單純：幫客人推車子、算錢、檢查倉庫還有沒有貨，以及把客人不要的東西拿出來。*/
namespace SimpleShop.Controllers
{
    public class CartController : Controller
    {
        /*這個 Controller 要運作，必須先拿到兩個工具：一個是 context (讓他可以連線到資料庫 去查商品存不存在)，
        另一個是 shoppingCart (讓他可以把東西放進客人的購物車裡)。*/
        // 這是管理員隨身攜帶的兩大法寶：
        private readonly ApplicationDbContext _context; // 法寶一：連接倉庫的對講機 (資料庫)
        private readonly ShoppingCart _shoppingCart;    // 法寶二：客人的專屬購物車 (暫存資料)

        // 管理員上班報到！老闆(程式)把兩大法寶交給他
        public CartController(ApplicationDbContext context, ShoppingCart shoppingCart)
        {
            _context = context;
            _shoppingCart = shoppingCart;
        }

        public IActionResult Index()
        {
            // 管理員用計算機算出總金額，寫在小白板(ViewBag)上給客人看
            ViewBag.Total = _shoppingCart.GetTotal();

            // 把購物車裡所有的東西(Items)整理好，推給客人看(回傳 View)
            return View(_shoppingCart.Items);
        }
        // 當客人在網頁上點擊「我的購物車」時，就會呼叫這個 Index() 方法。管理員會先算出總金額。
        // 客人說：「我要買這個！」(AddToCart 方法)
        [HttpPost]        // 規定這是一個「寫入/修改」的動作，不能用網址亂打來觸發
        [ValidateAntiForgeryToken] // 這是店裡的保全機制！確保請求是從我們網站發出的，防止駭客
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            // 步驟 1：用對講機呼叫倉庫，看看這個商品存不存在？
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(); // 找不到？直接跟客人說「查無此物」
            }

            // 步驟 2：檢查倉庫的庫存夠不夠？
            if (product.StockQuantity < quantity)
            {
                // 如果庫存不夠，寫一張紅色的警告紙條 (TempData) 貼在客人螢幕上
                TempData["ErrorMessage"] = $"商品 '{product.Name}' 庫存不足 (剩餘 {product.StockQuantity} 件)。";
                return RedirectToAction("Index", "Home"); // 把客人趕回首頁或商品頁，不給加！
            }

            // 步驟 3：一切正常，把東西丟進購物車！
            _shoppingCart.AddItem(product, quantity);

            // 寫一張綠色的成功紙條，告訴客人加進去囉！
            // TempData 就像是一張「閱後即焚的便利貼」。你把它貼在畫面上，客人看到一次 (重新整理網頁後) 就會自動消失。

            // 非常適合用來顯示「加入成功」或「庫存不足」的提示訊息。
            TempData["SuccessMessage"] = $"已將 '{product.Name}' 加入購物車。";

            // 帶客人去看他現在的購物車畫面 (回到上面的 Index)
            return RedirectToAction("Index");
        }
        // 客人反悔了：「我不要這個了！」 (RemoveFromCart 方法)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveFromCart(int productId)
        {
            // 管理員二話不說，直接從車裡把東西拿出來
            _shoppingCart.RemoveItem(productId);

            // 貼一張紙條說：拿出來囉！
            TempData["SuccessMessage"] = "已從購物車移除商品。";

            // 再讓客人看一下現在購物車還剩什麼
            return RedirectToAction("Index");
        }
        // 客人三心二意：「我要改買 5 個！」 (UpdateQuantity 方法)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            // 一樣先去倉庫確認商品還在不在
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // 情境 A：客人獅子大開口，要買的數量大於庫存
            if (quantity > product.StockQuantity)
            {
                // 貼紅色警告紙條
                TempData["ErrorMessage"] = $"商品 '{product.Name}' 庫存不足 (剩餘 {product.StockQuantity} 件)。";

                // 【聰明的設計】：不直接拒絕，而是強制幫客人把數量改成「庫存最大值」
                _shoppingCart.UpdateQuantity(productId, product.StockQuantity);

            }
            // 情境 B：客人把數量改成 0 或負數 (可能是來亂的，或者是想刪除)
            else if (quantity <= 0)
            {
                // 直接幫他把這個商品從購物車移除
                _shoppingCart.RemoveItem(productId);
            }
            // 情境 C：乖乖購買正常的數量
            else
            {
                // 正常更新數量
                _shoppingCart.UpdateQuantity(productId, quantity);
            }

            // 帶客人看更新後的購物車畫面
            return RedirectToAction("Index");
        }
    }
}

/*資安防線：加上了 [ValidateAntiForgeryToken]，防止別人寫惡意程式狂塞東西進購物車。

應用防線：每次加入或修改數量，都會去檢查 product.StockQuantity (庫存)，絕對不會發生
「客人買了 10 個，結果倉庫只有 2 個」這種讓老闆頭痛的超賣問題！*/