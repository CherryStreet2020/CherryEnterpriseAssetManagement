# DB snapshots needed from operator (psql)

Run these in the Replit shell against `$DATABASE_URL` and save the output files
in this folder so the next run can diff against them.

## At the START of every run

```bash
psql "$DATABASE_URL" -c '
SELECT "EventType", COUNT(*)
FROM "OutboxEvents"
GROUP BY "EventType"
ORDER BY "EventType";' > outbox-baseline.txt

psql "$DATABASE_URL" -c '
SELECT
  (SELECT COUNT(*) FROM "Assets")              AS assets,
  (SELECT COUNT(*) FROM "JournalEntries")      AS jes,
  (SELECT COUNT(*) FROM "OutboxEvents")        AS outbox,
  (SELECT COUNT(*) FROM "MaintenanceEvents")   AS maintenance_events,
  (SELECT COUNT(*) FROM "PurchaseOrders")      AS pos,
  (SELECT COUNT(*) FROM "GoodsReceipts")       AS receipts,
  (SELECT COUNT(*) FROM "ApInvoices")          AS ap_invoices,
  (SELECT COUNT(*) FROM "CIPProjects")         AS cip_projects,
  (SELECT COUNT(*) FROM "Items")               AS items,
  (SELECT COUNT(*) FROM "Vendors")             AS vendors;
' > db-baseline.txt
```

## At the END of every run

Re-run both, save as `outbox-final.txt` and `db-final.txt`. The combined report
will diff baseline vs final automatically.
