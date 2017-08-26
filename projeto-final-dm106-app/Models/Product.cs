using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projeto_final_dm106_app.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string nome { get; set; }

        [MaxLength(1024)]
        public string descricao { get; set; }

        [MaxLength(32)]
        public string cor { get; set; }

        [Required]
        [Index(IsUnique = true)]
        [MaxLength(256)]
        public string modelo { get; set; }

        [Required]
        [Index(IsUnique = true)]
        [StringLength(32)]
        public string codigo { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal preco { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal peso { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal altura { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal largura { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal comprimento { get; set; }

        [Required]
        [Range(0, Int32.MaxValue)]
        public decimal diametro { get; set; }

        [MaxLength(256)]
        public string url { get; set; }
    }
}