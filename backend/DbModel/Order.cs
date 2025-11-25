using System.ComponentModel.DataAnnotations.Schema;

namespace backend.DbModel
{
    [Table("Orders")]
    public class Order
    {
        public int Id { get; set; }
        // Foreign key to Product via SerialNo (principal key)
        public string? SerialNo { get; set; }
        public Product? Product { get; set; }

        public string? Customer { get; set; }
        public string? OrderNumber { get; set; }
        public string? ProvinceCity { get; set; }
        public string? Address { get; set; }
        // Stored as TEXT in DB although it represents a date
        public string? DeliveryDate { get; set; }
    }
}
