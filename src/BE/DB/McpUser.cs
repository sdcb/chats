using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("McpUser")]
[Index("McpId", Name = "IX_McpUser_McpId")]
[Index("UserId", Name = "IX_McpUser_UserId")]
public partial class McpUser
{
    [Key]
    public int Id { get; set; }

    public int McpId { get; set; }

    public int UserId { get; set; }

    [ForeignKey("McpId")]
    [InverseProperty("McpUsers")]
    public virtual Mcp Mcp { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("McpUsers")]
    public virtual User User { get; set; } = null!;
}
