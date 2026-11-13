# Entity Framework Core persistence

PostgreSQL is the only database. Catalog, file metadata, history, subscriptions,
notification outbox, comments and Desktop tracking all use EF Core and PostgresDbContext.

PostgreSQL tables, columns, keys, indexes and defaults are defined in PostgresModel.
InitialPostgres and PostgresDbContextModelSnapshot are generated EF migrations.
Startup.cs resolves PostgresDbContext in a service scope and calls Database.MigrateAsync
once before seeding and starting the application. Stores and background workers do
not initialize the database. There are no custom migration locks or InitializeAsync
methods in persistence contracts or implementations.
No store creates tables directly. The database starts from the single InitialPostgres
migration. Existing data from the former multi-database layout is intentionally not
migrated. Future schema changes should be generated with dotnet ef.

History uses tracked entities and LINQ. PostgreSQL-specific upserts, JSON predicates
and outbox FOR UPDATE SKIP LOCKED still use parameterized EF SQL; those operations
have not yet been converted to LINQ. Each operation owns its context.

Catalog IDs are PostgreSQL identity columns. Relations use foreign keys and a
`content_tags` many-to-many table. Dynamic file metadata is stored as JSONB in the
separate `content_file_metadata` table and remains linked to files by a unique FileId.
