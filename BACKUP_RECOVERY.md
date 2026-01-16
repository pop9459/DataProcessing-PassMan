# Backup & Recovery Guide

Scope is MySQL (prod/stage). Test/SQLite is excluded.

## Backup strategy
- Type: Logical backups with `mysqldump` (schema + data) plus binlog-based point-in-time recovery (PITR).
- Frequency: Full dump nightly; incremental via binlogs retained 7–14 days (adjust per storage).
- Retention: 14 days for full dumps by default; shorter for non-prod.
- Storage: Off-host, access-controlled (e.g., S3 bucket with bucket encryption + least-privilege IAM). Keep 1 local copy for quick restore if disk permits.
- Consistency: Use `--single-transaction --routines --triggers --events` for InnoDB to avoid downtime during dumps.
- Scope: All passman schemas (`passManDB`), including routines, triggers, views.

## Backup command (example)
```
mysqldump \
  --single-transaction --routines --triggers --events \
  --master-data=2 \
  --databases passManDB \
  | gzip > passManDB-$(date +%F).sql.gz
```
- `--master-data=2` captures binlog position for PITR.
- Schedule via cron/systemd timer in prod/stage.

## Binlog (incremental) setup
- Enable binary logging on MySQL (`log_bin`, `binlog_expire_logs_seconds` or `expire_logs_days`).
- Retain binlogs at least the same as backup retention to allow PITR.
- Store binlogs off-host if possible for DR.

## Recovery playbook (full + PITR)
1) Stop app writes (or put API in maintenance) to avoid divergence.
2) Restore latest full dump:
```
gunzip -c passManDB-YYYY-MM-DD.sql.gz | mysql -u root -p
```
3) Apply binlogs from the position in the dump (`--master-data=2` shows CHANGE MASTER TO / log file + pos):
```
mysqlbinlog --start-position=<pos> binlog.000123 | mysql -u root -p
```
4) Bring API back online; run smoke tests (auth, vault list, credential list).

## Downtime prevention
- Use `--single-transaction` to avoid table locks during backups.
- Run backups from a replica if available; otherwise run off-peak.
- Keep PITR binlogs so small rollbacks don’t require full restore.

## Verification
- After each backup, run `mysql --execute "USE passManDB; SHOW TABLES;"` against a restored copy or a throwaway container to ensure dump is readable.
- Periodically perform a restore drill to validate backup + binlogs and measure RTO/RPO.

## Access and security
- Credentials for backup/restore should be minimal-privilege (backup role with SELECT, SHOW VIEW, TRIGGER, LOCK TABLES if needed; SUPER not required).
- Encrypt at rest (storage) and in transit (TLS to bucket/remote).

## Notes for non-prod
- Dev/test using SQLite/in-memory need no backups.
- For local MySQL dev, ad-hoc dumps are sufficient; skip binlogs if not enabled.

