namespace RevEx.Infrastructure.Persistence.Migrations;

internal static class DesktopUpdateSchema
{
    // Idempotent additive migration, executed under a PostgreSQL advisory lock.
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS desktop_installations (
            installation_id uuid PRIMARY KEY,
            key_hash varchar(64) NOT NULL,
            platform varchar(32) NOT NULL,
            version varchar(32) NOT NULL,
            user_id varchar(300),
            user_name varchar(300),
            last_seen_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE TABLE IF NOT EXISTS desktop_update_offers (
            id uuid PRIMARY KEY,
            installation_id uuid NOT NULL REFERENCES desktop_installations(installation_id),
            from_version varchar(32) NOT NULL,
            policy jsonb NOT NULL,
            offered_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            confirmed_at timestamptz,
            user_id varchar(300),
            user_name varchar(300),
            failure text
        );
        CREATE TABLE IF NOT EXISTS desktop_downloads (
            id uuid PRIMARY KEY,
            offer_id uuid REFERENCES desktop_update_offers(id),
            installation_id uuid REFERENCES desktop_installations(installation_id),
            version varchar(32) NOT NULL,
            file_name varchar(180) NOT NULL,
            started_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            finished_at timestamptz,
            status varchar(32) NOT NULL DEFAULT 'started',
            user_id varchar(300),
            user_name varchar(300)
        );
        CREATE INDEX IF NOT EXISTS ix_desktop_installations_user ON desktop_installations(user_id);
        CREATE INDEX IF NOT EXISTS ix_desktop_update_offers_installation ON desktop_update_offers(installation_id, offered_at DESC);
        CREATE INDEX IF NOT EXISTS ix_desktop_downloads_time ON desktop_downloads(started_at DESC);
        """;
}
