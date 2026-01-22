using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("McpTool")]
[Index("McpServerId", "ToolName", Name = "UX_McpTool_Server_Name", IsUnique = true)]
public partial class McpTool
{
    [Key]
    public int Id { get; set; }

    public int McpServerId { get; set; }

    [StringLength(100)]
    public string ToolName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Parameters { get; set; }

    [ForeignKey("McpServerId")]
    [InverseProperty("McpTools")]
    public virtual McpServer McpServer { get; set; } = null!;
}
