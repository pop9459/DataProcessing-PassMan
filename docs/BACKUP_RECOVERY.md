# Backup & Recovery Guide

Scope is MySQL (prod/stage). Test/SQLite is excluded.

## Why these choices fit a password manager

PassMan stores sensitive credential data that users rely on exclusively — there is no fallback if a vault is lost. This makes data durability a higher priority than for typical CRUD applications. At the same time, the database is relatively small (users, vaults, vault shares, audit logs), so heavyweight enterprise backup tooling is unnecessary. The strategy below is calibrated to that profile: maximum recoverability at reasonable cost, with no scheduled downtime.

## Backup strategy

| Parameter | Choice | Reasoning |
|-----------|--------|-----------|
| **Type** | Logical (`mysqldump`) + binlog PITR | Logical dumps are portable, human-inspectable, and sufficient for a single-schema app of this size. Physical (xtrabackup) would add complexity without benefit at this scale. Binlogs add point-in-time precision so a corrupted or accidentally deleted vault can be recovered to the moment before the event. |
| **Frequency** | Full dump nightly; binlogs continuous | Nightly full dumps bound the worst-case restore time. Continuous binlogs close the gap — without them, up to 24 hours of credential changes could be lost. |
| **Retention** | 14 days full dumps; binlogs match retention | 14 days gives enough window to detect silent corruption or accidental bulk-deletes, which are often noticed days after the fact. Matching binlog retention ensures PITR is available across the full retention window. |
| **Storage** | Off-host (e.g. S3), encrypted, least-privilege | If the database host is lost or compromised, on-host backups are worthless. Off-host storage with bucket encryption protects backups at rest; least-privilege IAM prevents a compromised backup credential from being used to overwrite or delete backups. |
| **Consistency** | `--single-transaction` (InnoDB) | InnoDB's MVCC allows a consistent snapshot without table locks. Taking a lock-based dump on a live password manager would block every vault read/write during the backup window — unacceptable for a service users depend on at any hour. |

## Backup command (example)

```bash
mysqldump \
  --single-transaction --routines --triggers --events \
  --master-data=2 \
  --databases passManDB \
  | gzip > passManDB-$(date +%F).sql.gz
```

- `--single-transaction`: consistent InnoDB snapshot with no locks (see table above).
- `--routines --triggers --events`: ensures stored procedures, triggers and events are included so a restored database is fully functional without manual DDL.
- `--master-data=2`: embeds the binlog file name and position as a comment in the dump, which is required to resume PITR from exactly the right point.
- Schedule via cron/systemd timer in prod/stage; run off-peak to reduce I/O contention.

## Binlog (incremental) setup

Enable binary logging in MySQL (`log_bin`, `binlog_expire_logs_seconds`). Binlogs record every write in order, so they serve as the incremental layer between nightly fulls. Without them, recovery granularity is limited to the previous night's dump, meaning up to 24 h of credential data loss is possible. Retain binlogs at least as long as full dump retention to keep PITR available across the entire window. Store binlogs off-host for the same disaster-recovery reason as full dumps.

## Recovery playbook (full + PITR)

1. Stop app writes (or put the API in maintenance mode) to prevent new writes from conflicting with the restore.
2. Restore the latest full dump:
   ```bash
   gunzip -c passManDB-YYYY-MM-DD.sql.gz | mysql -u root -p
   ```
3. Apply binlogs from the position recorded in the dump (`--master-data=2` shows the log file and position):
   ```bash
   mysqlbinlog --start-position=<pos> binlog.000123 | mysql -u root -p
   ```
   To stop at a specific point in time (e.g. just before an accidental bulk-delete):
   ```bash
   mysqlbinlog --start-position=<pos> --stop-datetime="YYYY-MM-DD HH:MM:SS" binlog.000123 | mysql -u root -p
   ```
4. Bring the API back online and run smoke tests (auth, vault list, credential list, vault-share access).

## Downtime prevention

The primary mechanism is `--single-transaction`: because passManDB uses InnoDB exclusively, the backup reads a consistent snapshot without acquiring table locks, so reads and writes continue uninterrupted during the dump. If a replica is available, routing backup jobs there removes even the I/O load from the primary. Scheduling off-peak (e.g. 03:00) further reduces the risk of backup I/O affecting response times. PITR via binlogs means that in most scenarios only the binlogs since the last dump need to be replayed — keeping recovery time (RTO) short and avoiding a full-night restore for small incidents.

## Verification

Backups are only valuable if they can actually be restored. After each dump, run a quick smoke check against a throwaway container:

```bash
gunzip -c passManDB-YYYY-MM-DD.sql.gz | mysql -u root -p
mysql -u root -p passManDB --execute "SHOW TABLES; SELECT COUNT(*) FROM Vaults;"
```

Periodically (e.g. monthly) perform a full restore drill including binlog replay to validate the complete PITR chain and measure realistic RTO/RPO numbers. This is especially important for a credential store — discovering a broken restore process during an actual incident is far worse than the cost of a monthly drill.

## Access and security

Backup credentials should be minimal-privilege (SELECT, SHOW VIEW, TRIGGER, LOCK TABLES if needed; no SUPER). This limits the blast radius if backup credentials are leaked — an attacker gains read access to a dump but cannot modify the live database. Encrypt dumps in transit and at rest.

## Notes for non-prod

Dev/test using SQLite or in-memory databases need no backups. For local MySQL dev, ad-hoc dumps are sufficient; binlogs are not needed if PITR is not required.
