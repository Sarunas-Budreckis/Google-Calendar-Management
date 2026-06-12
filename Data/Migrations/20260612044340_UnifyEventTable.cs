using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoogleCalendarManagement.Data.Migrations
{
    /// <summary>
    /// Story 8.2 — merges gcal_event + pending_event into a single unified `event` table keyed by a
    /// stable local event_id, repoints gcal_event_version and the four source-table linked_event_id
    /// columns, and drops date_source_integration. This Up() is a hand-written, data-preserving SQL
    /// transform (the EF-scaffolded body was a destructive drop/recreate). It runs inside the single
    /// transaction EF wraps around the migration. Operations are ordered so no foreign key is ever
    /// violated regardless of whether FK enforcement is on, since SQLite cannot drop a FK in place:
    /// every child of gcal_event (gcal_event_version, toggl_data, pending_event) is rebuilt or dropped
    /// before gcal_event itself is dropped.
    ///
    /// IRREVERSIBLE: the old tables are dropped, so Down() cannot reconstruct lost rows. Recovery is
    /// via the automatic pre-migration backup taken by MigrationService.ApplyMigrationsAsync()
    /// (CreateBackupAsync("pre-migration")). REVISIT (Sarunas): confirm on a copy of the live DB first.
    /// </summary>
    public partial class UnifyEventTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Step A: create the unified `event` table (schema mirrors EventConfiguration) ----
            migrationBuilder.Sql(@"
CREATE TABLE ""event"" (
    ""event_id"" TEXT NOT NULL CONSTRAINT ""PK_event"" PRIMARY KEY,
    ""gcal_event_id"" TEXT NULL,
    ""calendar_id"" TEXT NOT NULL,
    ""summary"" TEXT NULL,
    ""description"" TEXT NULL,
    ""start_datetime"" TEXT NULL,
    ""end_datetime"" TEXT NULL,
    ""is_all_day"" INTEGER NULL,
    ""color_id"" TEXT NULL,
    ""lifecycle"" TEXT NOT NULL DEFAULT 'approved',
    ""publish"" TEXT NOT NULL DEFAULT 'local_only',
    ""has_unpublished_changes"" INTEGER NOT NULL DEFAULT 0,
    ""source_system"" TEXT NULL,
    ""recurring_event_id"" TEXT NULL,
    ""is_recurring_instance"" INTEGER NOT NULL DEFAULT 0,
    ""gcal_etag"" TEXT NULL,
    ""gcal_updated_at"" TEXT NULL,
    ""last_synced_at"" TEXT NULL,
    ""app_last_modified_at"" TEXT NULL,
    ""created_at"" TEXT NOT NULL,
    ""updated_at"" TEXT NOT NULL,
    CONSTRAINT ""CK_event_lifecycle"" CHECK (lifecycle IN ('candidate','approved')),
    CONSTRAINT ""CK_event_publish"" CHECK (publish IN ('local_only','published'))
);");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_event_date"" ON ""event"" (""start_datetime"", ""end_datetime"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""idx_event_day_name_unique"" ON ""event"" (""start_datetime"", ""source_system"") WHERE source_system = 'day_name';");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""idx_event_gcal_event_id"" ON ""event"" (""gcal_event_id"") WHERE gcal_event_id IS NOT NULL;");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_event_lifecycle"" ON ""event"" (""lifecycle"");");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_event_recurring"" ON ""event"" (""recurring_event_id"");");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_event_source"" ON ""event"" (""source_system"");");

            // ---- Step B: every gcal_event row -> event(approved, published). New stable event_id
            // per row; gcal_event_id preserved so the version/linked-id joins below resolve. The old
            // is_deleted flag has no column in the unified model (deleted events become deleted_event
            // rows in Story 8.6); all other content is preserved. ----
            migrationBuilder.Sql(@"
INSERT INTO ""event"" (
    event_id, gcal_event_id, calendar_id, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, lifecycle, publish, has_unpublished_changes, source_system,
    recurring_event_id, is_recurring_instance, gcal_etag, gcal_updated_at, last_synced_at,
    app_last_modified_at, created_at, updated_at)
SELECT
    lower(hex(randomblob(16))), gcal_event_id, calendar_id, summary, description, start_datetime,
    end_datetime, is_all_day, color_id, 'approved', 'published', 0, source_system,
    recurring_event_id, is_recurring_instance, gcal_etag, gcal_updated_at, last_synced_at,
    app_last_modified_at, created_at, updated_at
FROM gcal_event;");

            // ---- Step C: overlay pending_event rows (gcal_event_id set). Apply the local edits onto
            // the matching event row and mark it dirty. operation_type='delete' overlays keep the
            // published content (they are a staged delete, not an edit) but are still marked dirty. ----
            migrationBuilder.Sql(@"
UPDATE ""event""
SET summary        = (SELECT p.summary        FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    description    = (SELECT p.description    FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    start_datetime = (SELECT p.start_datetime FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    end_datetime   = (SELECT p.end_datetime   FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    is_all_day     = (SELECT p.is_all_day     FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    color_id       = (SELECT p.color_id       FROM pending_event p WHERE p.gcal_event_id = ""event"".gcal_event_id),
    has_unpublished_changes = 1
WHERE ""event"".gcal_event_id IN (
    SELECT gcal_event_id FROM pending_event
    WHERE gcal_event_id IS NOT NULL AND operation_type <> 'delete');");
            migrationBuilder.Sql(@"
UPDATE ""event""
SET has_unpublished_changes = 1
WHERE ""event"".gcal_event_id IN (
    SELECT gcal_event_id FROM pending_event
    WHERE gcal_event_id IS NOT NULL AND operation_type = 'delete');");

            // ---- Step D: manual pending drafts (no gcal id) -> event(approved, local_only).
            // event_id = pending_event_id so any linked_event_id pointing at the pending id stays valid. ----
            migrationBuilder.Sql(@"
INSERT INTO ""event"" (
    event_id, gcal_event_id, calendar_id, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, lifecycle, publish, has_unpublished_changes, source_system,
    recurring_event_id, is_recurring_instance, gcal_etag, gcal_updated_at, last_synced_at,
    app_last_modified_at, created_at, updated_at)
SELECT
    pending_event_id, NULL, calendar_id, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, 'approved', 'local_only', 0, source_system,
    NULL, 0, NULL, NULL, NULL, NULL, created_at, updated_at
FROM pending_event
WHERE gcal_event_id IS NULL AND (source_system = 'manual' OR source_system IS NULL);");

            // ---- Step E: machine-generated pending drafts (no gcal id) -> event(candidate, local_only). ----
            migrationBuilder.Sql(@"
INSERT INTO ""event"" (
    event_id, gcal_event_id, calendar_id, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, lifecycle, publish, has_unpublished_changes, source_system,
    recurring_event_id, is_recurring_instance, gcal_etag, gcal_updated_at, last_synced_at,
    app_last_modified_at, created_at, updated_at)
SELECT
    pending_event_id, NULL, calendar_id, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, 'candidate', 'local_only', 0, source_system,
    NULL, 0, NULL, NULL, NULL, NULL, created_at, updated_at
FROM pending_event
WHERE gcal_event_id IS NULL AND source_system IS NOT NULL AND source_system <> 'manual';");

            // ---- Step G: rewrite linked_event_id (gcal-id strings) to the stable event_id on the
            // four source tables. Values that were a pending_event_id already equal the new event_id
            // (steps D/E), so the guarded UPDATE leaves them untouched. Done before the toggl_data
            // rebuild so the new table simply copies the corrected values. ----
            foreach (var table in new[] { "toggl_data", "call_log_entry", "civ5_data", "comfyui_data" })
            {
                migrationBuilder.Sql($@"
UPDATE ""{table}""
SET linked_event_id = (SELECT e.event_id FROM ""event"" e WHERE e.gcal_event_id = ""{table}"".linked_event_id)
WHERE linked_event_id IS NOT NULL
  AND EXISTS (SELECT 1 FROM ""event"" e WHERE e.gcal_event_id = ""{table}"".linked_event_id);");
            }

            // ---- Step F: rebuild gcal_event_version so its FK points at event(event_id). SQLite
            // cannot change a column's FK in place, so recreate the table. Versions whose old
            // gcal_event_id has no matching event (orphans) are dropped (event_id is NOT NULL). ----
            migrationBuilder.Sql(@"ALTER TABLE ""gcal_event_version"" RENAME TO ""gcal_event_version_old"";");
            migrationBuilder.Sql(@"
CREATE TABLE ""gcal_event_version"" (
    ""version_id"" INTEGER NOT NULL CONSTRAINT ""PK_gcal_event_version"" PRIMARY KEY AUTOINCREMENT,
    ""event_id"" TEXT NOT NULL,
    ""gcal_etag"" TEXT NULL,
    ""summary"" TEXT NULL,
    ""description"" TEXT NULL,
    ""start_datetime"" TEXT NULL,
    ""end_datetime"" TEXT NULL,
    ""is_all_day"" INTEGER NULL,
    ""color_id"" TEXT NULL,
    ""gcal_updated_at"" TEXT NULL,
    ""recurring_event_id"" TEXT NULL,
    ""is_recurring_instance"" INTEGER NOT NULL DEFAULT 0,
    ""changed_by"" TEXT NULL,
    ""change_reason"" TEXT NULL,
    ""created_at"" TEXT NOT NULL,
    CONSTRAINT ""FK_gcal_event_version_event_event_id"" FOREIGN KEY (""event_id"") REFERENCES ""event"" (""event_id"") ON DELETE RESTRICT
);");
            migrationBuilder.Sql(@"
INSERT INTO ""gcal_event_version"" (
    version_id, event_id, gcal_etag, summary, description, start_datetime, end_datetime,
    is_all_day, color_id, gcal_updated_at, recurring_event_id, is_recurring_instance,
    changed_by, change_reason, created_at)
SELECT
    v.version_id,
    (SELECT e.event_id FROM ""event"" e WHERE e.gcal_event_id = v.gcal_event_id),
    v.gcal_etag, v.summary, v.description, v.start_datetime, v.end_datetime,
    v.is_all_day, v.color_id, v.gcal_updated_at, v.recurring_event_id, v.is_recurring_instance,
    v.changed_by, v.change_reason, v.created_at
FROM ""gcal_event_version_old"" v
WHERE EXISTS (SELECT 1 FROM ""event"" e WHERE e.gcal_event_id = v.gcal_event_id);");
            migrationBuilder.Sql(@"DROP TABLE ""gcal_event_version_old"";");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_version_event"" ON ""gcal_event_version"" (""event_id"", ""created_at"");");

            // ---- Step F2: rebuild toggl_data to drop its FK to gcal_event (published_gcal_event_id
            // stays as a plain scalar column; linking moves to the link table in Epic 8). ----
            migrationBuilder.Sql(@"ALTER TABLE ""toggl_data"" RENAME TO ""toggl_data_old"";");
            migrationBuilder.Sql(@"
CREATE TABLE ""toggl_data"" (
    ""toggl_id"" INTEGER NOT NULL CONSTRAINT ""PK_toggl_data"" PRIMARY KEY,
    ""description"" TEXT NULL,
    ""start_time"" TEXT NOT NULL,
    ""end_time"" TEXT NULL,
    ""duration_seconds"" INTEGER NULL,
    ""project_name"" TEXT NULL,
    ""tags"" TEXT NULL,
    ""visible_as_event"" INTEGER NOT NULL DEFAULT 1,
    ""published_to_gcal"" INTEGER NOT NULL DEFAULT 0,
    ""published_gcal_event_id"" TEXT NULL,
    ""last_synced_at"" TEXT NULL,
    ""created_at"" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ""toggl_data_type"" TEXT NULL,
    ""linked_event_id"" TEXT NULL,
    ""linked_event_type"" TEXT NULL
);");
            migrationBuilder.Sql(@"
INSERT INTO ""toggl_data"" (
    toggl_id, description, start_time, end_time, duration_seconds, project_name, tags,
    visible_as_event, published_to_gcal, published_gcal_event_id, last_synced_at, created_at,
    toggl_data_type, linked_event_id, linked_event_type)
SELECT
    toggl_id, description, start_time, end_time, duration_seconds, project_name, tags,
    visible_as_event, published_to_gcal, published_gcal_event_id, last_synced_at, created_at,
    toggl_data_type, linked_event_id, linked_event_type
FROM ""toggl_data_old"";");
            migrationBuilder.Sql(@"DROP TABLE ""toggl_data_old"";");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_toggl_date"" ON ""toggl_data"" (""start_time"", ""end_time"");");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_toggl_description"" ON ""toggl_data"" (""description"");");
            migrationBuilder.Sql(@"CREATE INDEX ""idx_toggl_type"" ON ""toggl_data"" (""toggl_data_type"");");

            // ---- Step H: drop the superseded tables. pending_event (child) and date_source_integration
            // first, then gcal_event once nothing references it. ----
            migrationBuilder.Sql(@"DROP TABLE ""pending_event"";");
            migrationBuilder.Sql(@"DROP TABLE ""gcal_event"";");
            migrationBuilder.Sql(@"DROP TABLE ""date_source_integration"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible. The unification drops gcal_event, pending_event and
            // date_source_integration; their rows cannot be reconstructed from the unified `event`
            // table. To roll back, restore the automatic pre-migration backup created by
            // MigrationService (see the class summary). No automatic Down is provided.
            throw new System.NotSupportedException(
                "UnifyEventTable (Story 8.2) is not reversible. Restore the pre-migration database backup instead.");
        }
    }
}
