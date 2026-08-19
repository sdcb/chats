using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("McpServer")]
[Index("OwnerUserId", "Name", Name = "UX_McpServer_Owner_Name", IsUnique = true)]
public partial class McpServer
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string? DisplayName { get; set; }

    [StringLength(300)]
    public string Url { get; set; } = null!;

    public string? Headers { get; set; }

    public DateTime CreatedAt { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ServerInstructions { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string Name { get; set; } = null!;

    [InverseProperty("McpServer")]
    public virtual ICollection<ChatConfigMcp> ChatConfigMcps { get; set; } = new List<ChatConfigMcp>();

    [InverseProperty("McpServer")]
    public virtual ICollection<McpTool> McpTools { get; set; } = new List<McpTool>();

    [ForeignKey("OwnerUserId")]
    [InverseProperty("McpServers")]
    public virtual User OwnerUser { get; set; } = null!;

    [InverseProperty("McpServer")]
    public virtual ICollection<UserMcp> UserMcps { get; set; } = new List<UserMcp>();
}
