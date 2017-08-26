using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace projeto_final_dm106_app.Models
{
    public class Order
    {
        public Order()
        {
            this.OrderItems = new HashSet<OrderItem>();
        }

        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string userEmail { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime data { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime dataEntrega { get; set; }

        [Required]
        [MaxLength(32)]
        public string status { get; set; }

        [Range(0, Int32.MaxValue)]
        public decimal precoTotal { get; set; }

        [Range(0, Int32.MaxValue)]
        public decimal pesoTotal { get; set; }

        [Range(0, Int32.MaxValue)]
        public decimal precoFrete { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }
}