using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("raks_aw")]
    public class RakAW
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("kode_rak")]
        public string? KodeRak { get; set; }

        [Column("full_qr")]
        public string? FullQR { get; set; }

        [Column("item_code")]
        public string? ItemCode { get; set; }
    }
}
