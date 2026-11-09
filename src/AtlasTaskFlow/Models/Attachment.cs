namespace AtlasTaskFlow.Models;

public class Attachment
{
    public int AttachmentId { get; set; }
    public int TaskId { get; set; }
    public int UploadedByUserId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public AttachmentType FileType { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The file itself, stored in the database (varbinary). Uploads are limited to 5 MB each, so this stays small enough
    /// for SQL Server. Not loaded when listing a task's files; only when downloading one.
    /// </summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
