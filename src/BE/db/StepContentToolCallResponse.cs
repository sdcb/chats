using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("StepContentToolCallResponse")]
public partial class StepContentToolCallResponse
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string ToolCallId { get; set; } = null!;

    public bool IsSuccess { get; set; }

    public string Response { get; set; } = null!;

    public int DurationMs { get; set; }

    [ForeignKey("Id")]
    [InverseProperty("StepContentToolCallResponse")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
