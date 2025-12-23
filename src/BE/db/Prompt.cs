using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("Prompt")]
[Index("Name", Name = "IX_Prompt2_Name")]
[Index("CreateUserId", Name = "IX_Prompt_CreateUserId")]
public partial class Prompt
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public string Content { get; set; } = null!;

    public float? Temperature { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int CreateUserId { get; set; }

    [ForeignKey("CreateUserId")]
    [InverseProperty("Prompts")]
    public virtual User CreateUser { get; set; } = null!;
}
