using Microsoft.EntityFrameworkCore;

namespace DomainPersistence.Entities;

/// <summary>
/// DMS mission-domain model configuration, kept apart from the GMS block so the
/// two can be read independently.
///
/// Two conventions run through all of it:
///   * Actor columns (WaivedBy, NominatedBy, ApprovedBy, ...) are RESTRICT — a
///     user who signed something off cannot be deleted out from under the record.
///   * Everything hanging off a mission or a participation is CASCADE, because it
///     has no meaning without its parent.
/// </summary>
public partial class ApplicationDBContext
{
    private static void ConfigureMissionDomain(ModelBuilder modelBuilder)
    {
        // ── Phase 1: the invitation from the host, and the mission it becomes ──
        modelBuilder.Entity<HostInvitation>(hi =>
        {
            hi.ToTable("HostInvitations");
            hi.HasKey(x => x.Id);
            hi.Property(x => x.HostOrganization).IsRequired().HasMaxLength(300);
            hi.Property(x => x.HostEmail).HasMaxLength(255);
            hi.Property(x => x.MissionTitle).IsRequired().HasMaxLength(300);
            hi.Property(x => x.AttachmentUrl).HasMaxLength(500);
            hi.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("logged");
            hi.Property(x => x.Notes).HasMaxLength(1000);
            hi.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            hi.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            // RESTRICT: an invitation that has become a mission is the mission's
            // provenance. Deleting it would orphan that link silently.
            hi.HasOne(x => x.ConvertedEvent)
                .WithMany()
                .HasForeignKey(x => x.ConvertedEventId)
                .OnDelete(DeleteBehavior.Restrict);

            // Destination is a Locations row, never free text — the place is
            // created there at log time if it is new. RESTRICT so a location that
            // an invitation points at cannot be deleted out from under it.
            hi.HasOne(x => x.Destination)
                .WithMany()
                .HasForeignKey(x => x.DestinationId)
                .OnDelete(DeleteBehavior.Restrict);

            // The host is an Organizations row. RESTRICT for the same reason as
            // the destination: an organisation named on an invitation cannot be
            // deleted while that invitation still points at it.
            hi.HasOne(x => x.HostOrganizationRef)
                .WithMany()
                .HasForeignKey(x => x.HostOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            hi.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.HostName).HasMaxLength(300);
            e.Property(x => x.HostEmail).HasMaxLength(255);

            // RESTRICT: an organisation still hosting a mission must not be
            // deletable out from under it.
            e.HasOne(x => x.HostOrganization)
                .WithMany()
                .HasForeignKey(x => x.HostOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DestinationTier).HasMaxLength(30);
            e.Property(x => x.FundingModel).HasMaxLength(20);
            // A reference into the finance system, which DMS does not own — so it
            // is stored as given, not validated against anything.
            e.Property(x => x.CostCenter).HasMaxLength(50);

            // SetNull, not Cascade: removing the invitation record must never take
            // the mission (and its whole delegation) with it.
            e.HasOne(x => x.HostInvitation)
                .WithMany()
                .HasForeignKey(x => x.HostInvitationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Destination is a Locations row. Nullable because a locally hosted
            // event travels nowhere; RESTRICT so a location a mission points at
            // cannot be deleted while still in use.
            e.HasOne(x => x.Destination)
                .WithMany()
                .HasForeignKey(x => x.DestinationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Phase 2: nomination + HR verification, both on the participation ───
        modelBuilder.Entity<EventGuest>(eg =>
        {
            eg.Property(x => x.Subgroup).HasMaxLength(100);

            // The delegate-type catalogue is Roles flagged IsDelegateRole — no
            // separate lookup table, so the same row that names the type also
            // carries whatever portal access it should come with.
            // RESTRICT: a role someone is nominated under must not be deletable
            // out from under the roster.
            eg.HasOne(x => x.MissionRole)
                .WithMany()
                .HasForeignKey(x => x.MissionRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            eg.Property(x => x.HrVerificationNote).HasMaxLength(500);
            eg.Property(x => x.VisaRequired).HasDefaultValue(true);
            eg.Property(x => x.HrVerificationStatus).HasMaxLength(20).HasDefaultValue("pending");

            eg.HasOne(x => x.NominatedByUser)
                .WithMany()
                .HasForeignKey(x => x.NominatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            eg.HasOne(x => x.HrVerifiedByUser)
                .WithMany()
                .HasForeignKey(x => x.HrVerifiedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // RESTRICT: a group somebody is nominated into must not vanish out
            // from under the roster. Renaming it is the supported edit.
            eg.HasOne(x => x.Group)
                .WithMany(x => x.EventGuests)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // The roster screens all filter "who is on this mission, in this
            // subgroup" — worth an index once a mission carries real numbers.
            eg.HasIndex(x => new { x.EventId, x.Subgroup });
            // Same question asked through the FK, which is what the transport
            // group booking resolves members by.
            eg.HasIndex(x => new { x.EventId, x.GroupId });
        });

        modelBuilder.Entity<Group>(g =>
        {
            g.ToTable("Groups");
            g.HasKey(x => x.Id);
            g.Property(x => x.Name).IsRequired().HasMaxLength(100);
            g.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            g.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");
            // Two groups with one name would make the dropdown unusable, and the
            // backfill below collapses existing text on exactly this key.
            g.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
            g.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<Guest>(g =>
        {
            g.Property(x => x.JobTitle).HasMaxLength(150);
        });

        // ── Phase 4: the nomination letter and its versions ────────────────────
        modelBuilder.Entity<NominationLetter>(nl =>
        {
            nl.ToTable("NominationLetters");
            nl.HasKey(x => x.Id);
            nl.Property(x => x.Status).IsRequired().HasMaxLength(30).HasDefaultValue("not_generated");
            nl.Property(x => x.Language).HasMaxLength(5);
            nl.Property(x => x.MessageId).HasMaxLength(200);
            nl.Property(x => x.HostNotes).HasMaxLength(2000);
            nl.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            nl.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            nl.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // One letter per mission. Filtered so a soft-deleted one can be
            // re-created, same pattern as EventGuests.
            nl.HasIndex(x => x.EventId).IsUnique().HasFilter("[IsDeleted] = 0");

            nl.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<NominationLetterVersion>(v =>
        {
            v.ToTable("NominationLetterVersions");
            v.HasKey(x => x.Id);
            v.Property(x => x.Language).HasMaxLength(5);
            v.Property(x => x.DocumentUrl).HasMaxLength(500);
            v.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            v.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            v.HasOne(x => x.NominationLetter)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.NominationLetterId)
                .OnDelete(DeleteBehavior.Cascade);

            v.HasIndex(x => new { x.NominationLetterId, x.Version })
                .IsUnique().HasFilter("[IsDeleted] = 0");

            v.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<NominationLetterHistory>(h =>
        {
            h.ToTable("NominationLetterHistory");
            h.HasKey(x => x.Id);
            h.Property(x => x.Action).IsRequired().HasMaxLength(200);
            h.Property(x => x.Note).HasMaxLength(1000);
            h.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            h.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            h.HasOne(x => x.NominationLetter)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.NominationLetterId)
                .OnDelete(DeleteBehavior.Cascade);

            h.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            h.HasIndex(x => new { x.NominationLetterId, x.OccurredOn });
            h.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Phase 6: readiness waivers ────────────────────────────────────────
        modelBuilder.Entity<ReadinessWaiver>(w =>
        {
            w.ToTable("ReadinessWaivers");
            w.HasKey(x => x.Id);
            w.Property(x => x.ItemKey).IsRequired().HasMaxLength(30);
            // Required at the database level too, not just in the service: an
            // unexplained waiver is the one thing this table exists to prevent.
            w.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            w.Property(x => x.WaivedAt).HasDefaultValueSql("(sysutcdatetime())");
            w.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            w.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            w.HasOne(x => x.EventGuest)
                .WithMany(x => x.ReadinessWaivers)
                .HasForeignKey(x => x.EventGuestId)
                .OnDelete(DeleteBehavior.Cascade);

            w.HasOne(x => x.WaivedByUser)
                .WithMany()
                .HasForeignKey(x => x.WaivedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // One waiver per (delegate, mission, item) — the mission is implied by
            // EventGuestId, so this pair is the whole rule.
            w.HasIndex(x => new { x.EventGuestId, x.ItemKey })
                .IsUnique().HasFilter("[IsDeleted] = 0");

            w.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Phase 7: on-mission ops ───────────────────────────────────────────
        modelBuilder.Entity<Incident>(i =>
        {
            i.ToTable("Incidents");
            i.HasKey(x => x.Id);
            i.Property(x => x.Category).IsRequired().HasMaxLength(30);
            i.Property(x => x.Severity).IsRequired().HasMaxLength(10);
            i.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("open");
            i.Property(x => x.Description).HasMaxLength(2000);
            i.Property(x => x.RaisedVia).HasMaxLength(20).HasDefaultValue("portal");
            i.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            i.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            i.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: removing someone from the roster must not
            // erase the incidents raised about them.
            i.HasOne(x => x.EventGuest)
                .WithMany()
                .HasForeignKey(x => x.EventGuestId)
                .OnDelete(DeleteBehavior.Restrict);

            i.HasOne(x => x.RaisedByUser)
                .WithMany()
                .HasForeignKey(x => x.RaisedBy)
                .OnDelete(DeleteBehavior.Restrict);

            i.HasOne(x => x.ResolvedByUser)
                .WithMany()
                .HasForeignKey(x => x.ResolvedBy)
                .OnDelete(DeleteBehavior.Restrict);

            i.HasIndex(x => new { x.EventId, x.Status });
            i.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<FieldDecision>(fd =>
        {
            fd.ToTable("FieldDecisions");
            fd.HasKey(x => x.Id);
            fd.Property(x => x.DecisionNote).IsRequired().HasMaxLength(2000);
            fd.Property(x => x.DecidedAt).HasDefaultValueSql("(sysutcdatetime())");
            fd.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            fd.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            fd.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            fd.HasOne(x => x.DecidedByUser)
                .WithMany()
                .HasForeignKey(x => x.DecidedBy)
                .OnDelete(DeleteBehavior.Restrict);

            fd.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<GatheringNotification>(gn =>
        {
            gn.ToTable("GatheringNotifications");
            gn.HasKey(x => x.Id);
            gn.Property(x => x.Message).IsRequired().HasMaxLength(1000);
            gn.Property(x => x.Subgroup).HasMaxLength(100);
            gn.Property(x => x.SentAt).HasDefaultValueSql("(sysutcdatetime())");
            gn.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            gn.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            gn.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            gn.HasOne(x => x.SentByUser)
                .WithMany()
                .HasForeignKey(x => x.SentBy)
                .OnDelete(DeleteBehavior.Restrict);

            gn.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        // ── Phase 9: reports ──────────────────────────────────────────────────
        modelBuilder.Entity<PostMissionReport>(r =>
        {
            r.ToTable("PostMissionReports");
            r.HasKey(x => x.Id);
            r.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("not_submitted");
            r.Property(x => x.NudgeCount).HasDefaultValue(0);
            r.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            r.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            r.HasOne(x => x.EventGuest)
                .WithOne(x => x.PostMissionReport)
                .HasForeignKey<PostMissionReport>(x => x.EventGuestId)
                .OnDelete(DeleteBehavior.Cascade);

            r.HasIndex(x => x.EventGuestId).IsUnique().HasFilter("[IsDeleted] = 0");
            r.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });

        modelBuilder.Entity<CombinedReport>(cr =>
        {
            cr.ToTable("CombinedReports");
            cr.HasKey(x => x.Id);
            cr.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("draft");
            cr.Property(x => x.PublishedUrl).HasMaxLength(500);
            cr.Property(x => x.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            cr.Property(x => x.IsDeleted).HasDefaultValueSql("((0))");

            cr.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            cr.HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict);

            cr.HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            cr.HasIndex(x => x.EventId).IsUnique().HasFilter("[IsDeleted] = 0");
            cr.HasQueryFilter(x => x.IsDeleted == null || x.IsDeleted == false);
        });
    }
}
