using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("Mcp")]
public partial class Mcp
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string ServerLabel { get; set; } = null!;

    [StringLength(300)]
    public string ServerUrl { get; set; } = null!;

    public byte RequireApproval { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte IsPublic { get; set; }

    [InverseProperty("Mcp")]
    public virtual ICollection<ChatConfigMcp> ChatConfigMcps { get; set; } = new List<ChatConfigMcp>();

    [InverseProperty("Mcp")]
    public virtual ICollection<McpHeader> McpHeaders { get; set; } = new List<McpHeader>();

    [InverseProperty("Mcp")]
    public virtual ICollection<McpUser> McpUsers { get; set; } = new List<McpUser>();
}
