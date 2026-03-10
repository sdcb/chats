using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("RequestTrace")]
[Index("ScheduledDeleteAt", Name = "IX_RequestTrace_ScheduledDeleteAt_NotNull")]
[Index("TraceId", Name = "IX_RequestTrace_TraceId")]
[Index("UserId", Name = "IX_RequestTrace_UserId")]
public partial class RequestTrace
{
    [Key]
    public Guid Id { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? RequestBodyAt { get; set; }

    public DateTime? ResponseHeaderAt { get; set; }

    public DateTime? ResponseBodyAt { get; set; }

    public byte Direction { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    public int? UserId { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? TraceId { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string Method { get; set; } = null!;

    [StringLength(2048)]
    public string Url { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string? RequestContentType { get; set; }

    [StringLength(200)]
    [Unicode(false)]
    public string? ResponseContentType { get; set; }

    public short? StatusCode { get; set; }

    [StringLength(50)]
    public string? ErrorType { get; set; }

    public int RawRequestBodyBytes { get; set; }

    public int? RawResponseBodyBytes { get; set; }

    public int RequestBodyLength { get; set; }

    public int? ResponseBodyLength { get; set; }

    public DateTime? ScheduledDeleteAt { get; set; }

    [InverseProperty("Log")]
    public virtual RequestTracePayload? RequestTracePayload { get; set; }
}
