using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("RequestTrace")]
[Index("StartedAt", Name = "IX_RequestTrace_StartedAt")]
[Index("UserId", Name = "IX_RequestTrace_UserId")]
public partial class RequestTrace
{
    [Key]
    public long Id { get; set; }

    public DateTime StartedAt { get; set; }

    public int DurationMs { get; set; }

    public byte Direction { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    public int? UserId { get; set; }

    [StringLength(100)]
    public string? TraceId { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string Method { get; set; } = null!;

    [StringLength(2048)]
    public string Url { get; set; } = null!;

    [StringLength(200)]
    public string? RequestContentType { get; set; }

    [StringLength(200)]
    public string? ResponseContentType { get; set; }

    public short? StatusCode { get; set; }

    [StringLength(50)]
    public string? ErrorType { get; set; }

    public string? ErrorMessage { get; set; }

    public int RawRequestBodyBytes { get; set; }

    public int? RawResponseBodyBytes { get; set; }

    public bool IsRequestBodyTruncated { get; set; }

    public bool IsResponseBodyTruncated { get; set; }

    [InverseProperty("Log")]
    public virtual RequestTracePayload? RequestTracePayload { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RequestTraces")]
    public virtual User? User { get; set; }
}
