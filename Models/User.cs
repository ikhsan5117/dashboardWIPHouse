using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DashboardWIPHouse.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("password")]
        public string Password { get; set; }

        [Column("created_date")]
        public DateTime CreatedDate { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }
}
