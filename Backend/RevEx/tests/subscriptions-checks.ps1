$ErrorActionPreference = 'Stop'
$source = Get-Content "$PSScriptRoot/../RevEx.Api/Subscriptions/ContentSubscriptions.cs" -Raw
$schema = [regex]::Match($source, '(?s)new NpgsqlCommand\("""(.*?)""", connection\);').Groups[1].Value
if (!$schema) { throw 'Schema not found' }
$sql = @"
BEGIN;
$schema
INSERT INTO content_subscriptions(content_id,user_id,email) VALUES(-991,'check-user','test@example.com');
INSERT INTO content_subscriptions(content_id,user_id,email) VALUES(-991,'check-user','test@example.com')
ON CONFLICT(content_id,user_id) DO UPDATE SET email=EXCLUDED.email;
INSERT INTO content_history_events(content_id,description,author) VALUES(-991,'check change','test');
DO `$`$ BEGIN
IF (SELECT count(*) FROM content_notification_queue WHERE user_id='check-user') <> 1
THEN RAISE EXCEPTION 'Expected one notification'; END IF;
END `$`$;
DELETE FROM content_subscriptions WHERE user_id='check-user';
INSERT INTO content_history_events(content_id,description,author) VALUES(-991,'after unsubscribe','test');
DO `$`$ BEGIN
IF (SELECT count(*) FROM content_notification_queue WHERE user_id='check-user') <> 1
THEN RAISE EXCEPTION 'Unsubscribed user received notification'; END IF;
END `$`$;
ROLLBACK;
"@
$sql | docker exec -i revexweb-history-postgres-1 psql -U revex -d revex_history -v ON_ERROR_STOP=1
if ($LASTEXITCODE) { throw 'Subscription checks failed' }


