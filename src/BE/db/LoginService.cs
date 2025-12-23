using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chats.DB;

[Table("LoginService")]
public partial class LoginService
{
    [Key]
    public int Id { get; set; }

    [StringLength(1000)]
    public string Type { get; set; } = null!;

    public bool Enabled { get; set; }

    [StringLength(2048)]
    public string Configs { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
