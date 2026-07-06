using System.ComponentModel.DataAnnotations;

namespace SimpleShop.Models
{
    public class OrderCheckoutViewModel
    {
        [Required(ErrorMessage = "請填寫收貨地址")]
        [Display(Name = "收貨地址")] // 為了<label asp-for="..."> 顯示中文
        public string ShippingAddress { get; set; } = string.Empty;

        [Display(Name = "備註")] // 添加其他訊息屬性
        public string?Notes { get; set; }
    }
}