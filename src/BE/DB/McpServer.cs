using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("McpServer")]
public partial class McpServer
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Label { get; set; } = null!;

    [StringLength(300)]
    public string Url { get; set; } = null!;

    public bool RequireApproval { get; set; }

    public string? Headers { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsPublic { get; set; }

    [InverseProperty("McpServer")]
    public virtual ICollection<ChatConfigMcp> ChatConfigMcps { get; set; } = new List<ChatConfigMcp>();

    [InverseProperty("McpServer")]
    public virtual ICollection<UserMcp> UserMcps { get; set; } = new List<UserMcp>();
}
