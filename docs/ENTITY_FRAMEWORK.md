# Entity Framework Core persistence

MongoDB remains the catalog store; PostgreSQL remains the history, subscription,
notification outbox and Desktop tracking store. Connection settings and existing names remain unchanged.

PostgreSQL tables, columns, keys, indexes and defaults are defined in PostgresModel.
InitialPostgres and PostgresDbContextModelSnapshot are generated EF migrations.
Startup.cs resolves PostgresDbContext in a service scope and calls Database.MigrateAsync
once before seeding and starting the application. Stores and background workers do
not initialize the database. There are no custom migration locks or InitializeAsync
methods in persistence contracts or implementations.
No store creates tables directly. An adoption SQL generator tolerates existing
legacy tables and indexes; a separate compatibility migration upgrades older columns.
Existing data is preserved. Future schema changes should be generated with dotnet ef.

History uses tracked entities and LINQ. PostgreSQL-specific upserts, JSON predicates
and outbox FOR UPDATE SKIP LOCKED still use parameterized EF SQL; those operations
have not yet been converted to LINQ. Each operation owns its context.

MongoDbContext uses the MongoDB EF provider, retaining integer IDs and document shape.
Atomic ID allocation uses the driver escape hatch. Automatic transactions are disabled
for the standalone Compose MongoDB; cascade operations retain partial-failure limitations.
