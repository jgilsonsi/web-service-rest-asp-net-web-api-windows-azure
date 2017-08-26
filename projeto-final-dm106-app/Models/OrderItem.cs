using System.ComponentModel.DataAnnotations;

namespace projeto_final_dm106_app.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        [Range(0, 99999)]
        public int quantidade { get; set; }

        public int ProductId { get; set; }

        public int OrderId { get; set; }

        public virtual Product Product { get; set; }
    }
}