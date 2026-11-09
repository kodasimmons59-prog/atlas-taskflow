using AtlasTaskFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Data;

/// <summary>
/// The Entity Framework Core session with the Atlas database. Each DbSet is a table; OnModelCreating sets the keys,
/// relationships, sizes and indexes so the schema enforces the same rules as the forms.
///
/// Sprint 6: it also holds the ASP.NET Core Identity sign-in tables (AspNetUsers and friends) through
/// <see cref="IdentityUserContext{TUser}"/>. Atlas has no site-wide roles, so the Identity role tables are left out;
/// a person's role is per team, in TeamMembers.
/// </summary>
public class AtlasDbContext : IdentityUserContext<AtlasAccount>
{
    public AtlasDbContext(DbContextOptions<AtlasDbContext> options) : base(options) { }

    /// <summary>For <see cref="SqliteAtlasDbContext"/>, which keeps its own set of migrations.</summary>
    protected AtlasDbContext(DbContextOptions options) : base(options) { }

    /// <summary>Atlas people (profiles). Sign-in accounts are <see cref="IdentityUserContext{TUser}.Users"/>'s
    /// AspNetUsers table, reached through <see cref="Accounts"/>.</summary>
    public new DbSet<User> Users => Set<User>();
    public DbSet<AtlasAccount> Accounts => Set<AtlasAccount>();
    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as their names ("NeedsReview") rather than numbers, so the tables read clearly and
        // reordering an enum can't silently change saved data.
        configurationBuilder.Properties<TaskItemStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<TeamRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AttachmentType>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // The Identity tables first, then Atlas's own.
        base.OnModelCreating(model);

        // Users are never deleted in this sprint, and deleting one should never silently remove a team's work,
        // so every foreign key to Users is Restrict.

        model.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
        });

        // Every account belongs to one Atlas person, and a person has at most one account.
        model.Entity<AtlasAccount>(e =>
        {
            e.HasOne<User>().WithOne().HasForeignKey<AtlasAccount>(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.UserId).IsUnique();
        });

        model.Entity<TeamInvitation>(e =>
        {
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.HasOne<Team>().WithMany().HasForeignKey(i => i.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
            // One open invitation per email address per team.
            e.HasIndex(i => new { i.TeamId, i.Email }).IsUnique();
            e.HasIndex(i => i.Email);
        });

        model.Entity<Team>(e =>
        {
            e.Property(t => t.TeamName).HasMaxLength(100).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<TeamMember>(e =>
        {
            e.HasOne<Team>().WithMany().HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
            // A person belongs to a team once, with one role.
            e.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
        });

        model.Entity<Project>(e =>
        {
            e.Property(p => p.ProjectName).HasMaxLength(80).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasOne<Team>().WithMany().HasForeignKey(p => p.TeamId).OnDelete(DeleteBehavior.Cascade);
            // Project names are unique within a team (the form checks this too).
            e.HasIndex(p => new { p.TeamId, p.ProjectName }).IsUnique();
        });

        model.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(t => t.TaskId);
            e.Property(t => t.Title).HasMaxLength(100).IsRequired();
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.DueDate).HasColumnType("date");
            e.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Assignees).WithOne().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.ProjectId, t.Status });
        });

        model.Entity<TaskAssignee>(e =>
        {
            e.HasKey(a => new { a.TaskId, a.UserId });
            e.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Comment>(e =>
        {
            e.Property(c => c.MessageText).HasMaxLength(1000).IsRequired();
            e.HasOne<TaskItem>().WithMany().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Attachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            e.Property(a => a.Content).IsRequired();
            e.HasOne<TaskItem>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Notifications and activity lines outlive a deleted task (the message still makes sense), so their TaskId
        // is cleared rather than the row removed. SQL Server refuses a second cascade path from Teams, so the
        // clearing is done by the app (ClientSetNull) in AtlasStore.DeleteTask.
        model.Entity<Notification>(e =>
        {
            e.Property(n => n.Message).HasMaxLength(500).IsRequired();
            e.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Team>().WithMany().HasForeignKey(n => n.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TaskItem>().WithMany().HasForeignKey(n => n.TaskId).OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(n => new { n.UserId, n.TeamId, n.IsRead });
        });

        model.Entity<ActivityEntry>(e =>
        {
            e.Property(a => a.Message).HasMaxLength(500).IsRequired();
            e.HasOne<Team>().WithMany().HasForeignKey(a => a.TeamId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TaskItem>().WithMany().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(a => new { a.TeamId, a.CreatedAt });
        });
    }
}
