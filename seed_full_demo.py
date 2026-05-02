#!/usr/bin/env python3
"""
=============================================================================
CAUTION: Consider using /Admin/DataImport seed pipelines instead.
=============================================================================

This script uses ON CONFLICT DO NOTHING (safer than DELETE), but lacks
environment gating. Prefer the C# pipelines for production-safe seeding.

This script has been marked for review as of 2026-01-22 (Phase 1 Safety Hardening).

=============================================================================
"""

import os
import sys

# =============================================================================
# SAFETY GUARD: Require LAB/Development environment
# =============================================================================
def check_safety_guards():
    """Abort unless in LAB/Development environment."""
    
    env = os.environ.get('ASPNETCORE_ENVIRONMENT', '')
    if env.lower() not in ('development', 'lab', ''):
        print("=" * 70)
        print("BLOCKED: This script can only run in Development/LAB environment.")
        print(f"Current ASPNETCORE_ENVIRONMENT: {env}")
        print("=" * 70)
        print()
        print("Use /Admin/DataImport in the web UI instead.")
        sys.exit(1)
    
    if not env:
        print("WARNING: ASPNETCORE_ENVIRONMENT not set. Assuming Development.")

# Run safety check immediately on import
check_safety_guards()

import psycopg2
from datetime import datetime, timedelta
import random
import hashlib

def get_connection():
    return psycopg2.connect(
        host=os.environ.get('PGHOST'),
        port=os.environ.get('PGPORT'),
        user=os.environ.get('PGUSER'),
        password=os.environ.get('PGPASSWORD'),
        database=os.environ.get('PGDATABASE')
    )

def seed_inventory_data(cur):
    """Seed inventory lists and scans"""
    print("Seeding inventory tracking data...")
    
    cur.execute("SELECT \"Id\" FROM \"Assets\" LIMIT 50")
    assets = [row[0] for row in cur.fetchall()]
    
    if not assets:
        print("  No assets found, skipping inventory seeding")
        return
    
    # InventoryStatus: Draft=0, InProgress=1, Completed=2
    inventory_lists = [
        ("Q4-2025 Annual Inventory", "Completed inventory count for year-end", 2, "admin", "Building A", 45, 42, 2, 1),
        ("Q1-2026 Quarterly Count", "First quarter inventory verification", 1, "accountant", "Building B", 30, 18, 0, 0),
        ("2026 Warehouse Audit", "Full warehouse physical count", 0, "admin", "Warehouse", 25, 0, 0, 0),
    ]
    
    for name, desc, status, assigned, location, total, scanned, missing, found in inventory_lists:
        started = datetime.now() - timedelta(days=random.randint(1, 30)) if status != 0 else None
        completed = datetime.now() - timedelta(days=random.randint(1, 10)) if status == 2 else None
        
        cur.execute("""
            INSERT INTO "InventoryLists" ("Name", "Description", "Status", "CreatedDate", "StartedDate", "CompletedDate", 
                "AssignedTo", "Location", "TotalAssets", "ScannedAssets", "MissingAssets", "FoundAssets")
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT DO NOTHING
            RETURNING "Id"
        """, (
            name, desc, status,
            datetime.now() - timedelta(days=random.randint(30, 60)),
            started, completed, assigned, location, total, scanned, missing, found
        ))
        result = cur.fetchone()
        if result:
            list_id = result[0]
            # Status 1=InProgress, 2=Completed
            if status in (1, 2):
                for asset_id in random.sample(assets, min(15, len(assets))):
                    # AssetCondition: Excellent=0, Good=1, Fair=2, Poor=3
                    # ScanResult: Found=0, Missing=1
                    cur.execute("""
                        INSERT INTO "InventoryScans" ("InventoryListId", "AssetId", "ScannedBarcode", "ScanDate", 
                            "ScannedBy", "Location", "Result", "Condition", "Notes")
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                        ON CONFLICT DO NOTHING
                    """, (
                        list_id, asset_id, f"CFA{asset_id:08d}",
                        datetime.now() - timedelta(days=random.randint(1, 10)),
                        assigned, location, random.choice([0, 0, 0, 1]),
                        random.choice([0, 1, 2, 3]),
                        random.choice(["", "Located in expected bay", "Moved to new location", ""])
                    ))
    
    # BarcodeType: Code128=0, Code39=1, QR=2, EAN13=3
    # AssetCondition: Excellent=0, Good=1, Fair=2, Poor=3, Damaged=4, NeedsRepair=5
    for i, asset_id in enumerate(assets[:30]):
        cur.execute("""
            INSERT INTO "AssetInventories" ("AssetId", "BarcodeNumber", "BarcodeType", "LastScanDate", 
                "LastScannedBy", "Condition", "IsReconciled")
            VALUES (%s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT ("AssetId") DO NOTHING
        """, (
            asset_id,
            f"CFA{asset_id:08d}",
            random.choice([0, 1, 2, 3]),
            datetime.now() - timedelta(days=random.randint(1, 90)),
            random.choice(["admin", "accountant"]),
            random.choice([0, 1, 2, 5]),
            random.choice([True, True, False])
        ))
    
    print(f"  Created 3 inventory lists with scans and 30 barcode records")

def seed_maintenance_data(cur):
    """Seed maintenance events and schedules"""
    print("Seeding maintenance data...")
    
    cur.execute('SELECT "Id", "Description" FROM "Assets" WHERE "AcquisitionCost" > 50000 LIMIT 25')
    assets = cur.fetchall()
    
    if not assets:
        print("  No high-value assets found, skipping maintenance seeding")
        return
    
    # MaintenanceType: Preventative=0, Corrective=1, Predictive=2, Emergency=3, Inspection=4, Calibration=5
    # MaintenancePriority: Low=0, Medium=1, High=2, Critical=3
    # MaintenanceStatus: Scheduled=0, InProgress=1, Completed=2, Cancelled=3, Overdue=4
    maintenance_types = [0, 1, 2, 3, 4, 5]
    priorities = [0, 1, 2, 3]
    statuses = [0, 1, 2, 4]  # Scheduled, InProgress, Completed, Overdue
    
    events_created = 0
    for asset_id, description in assets:
        for _ in range(random.randint(1, 3)):
            status = random.choice(statuses)
            scheduled = datetime.now() + timedelta(days=random.randint(-30, 60))
            completed = None
            actual_cost = None
            
            if status == 2:  # Completed
                scheduled = datetime.now() - timedelta(days=random.randint(5, 60))
                completed = scheduled + timedelta(days=random.randint(0, 3))
                actual_cost = random.randint(100, 5000)
            elif status == 4:  # Overdue
                scheduled = datetime.now() - timedelta(days=random.randint(1, 15))
            
            cur.execute("""
                INSERT INTO "MaintenanceEvents" ("AssetId", "Type", "Description", "Priority", "Status",
                    "ScheduledDate", "CompletedDate", "EstimatedCost", "ActualCost", "TechnicianName", 
                    "WorkOrderNumber", "CreatedAt", "CreatedBy")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT DO NOTHING
            """, (
                asset_id,
                random.choice(maintenance_types),
                f"Maintenance for {description[:50]}",
                random.choice(priorities),
                status,
                scheduled,
                completed,
                random.randint(200, 3000),
                actual_cost,
                random.choice(["John Smith", "Mike Johnson", "Sarah Wilson"]),
                f"WO-{random.randint(1000, 9999)}",
                datetime.now() - timedelta(days=random.randint(1, 90)),
                "admin"
            ))
            events_created += 1
    
    # RecurrenceType: Daily=0, Weekly=1, Monthly=2, Quarterly=3, Annually=5
    recurrences = [0, 1, 2, 3, 5]
    for asset_id, description in assets[:10]:
        maint_type = random.choice(maintenance_types)
        cur.execute("""
            INSERT INTO "MaintenanceSchedules" ("AssetId", "Name", "Description", "Type", "Recurrence", 
                "IntervalValue", "StartDate", "NextDueDate", "EstimatedCost", "IsActive")
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT DO NOTHING
        """, (
            asset_id,
            f"Regular Maintenance Schedule",
            f"Scheduled maintenance for {description[:30]}",
            maint_type,
            random.choice(recurrences),
            random.randint(1, 12),
            datetime.now() - timedelta(days=random.randint(30, 180)),
            datetime.now() + timedelta(days=random.randint(7, 90)),
            random.randint(500, 5000),
            True
        ))
    
    print(f"  Created {events_created} maintenance events and 10 schedules")

def seed_cip_data(cur):
    """Seed CIP projects and costs"""
    print("Seeding CIP project data...")
    
    # CipProjectStatus: Planned=0, Active=1, OnHold=2, Completed=3, Cancelled=4, Capitalized=5
    # CipCostType: Construction=0, Engineering=1, Equipment=2, Labor=3, Materials=4, Freight=5, Installation=6, Testing=7, Permits=8
    projects = [
        ("CIP-2025-001", "New Assembly Line Installation", 0, 2500000, "Building A expansion with new robotic assembly line", "Engineering"),
        ("CIP-2025-002", "HVAC System Upgrade", 1, 850000, "Complete HVAC replacement for Buildings A and B", "Facilities"),
        ("CIP-2025-003", "Server Room Expansion", 1, 320000, "Data center capacity expansion", "IT"),
        ("CIP-2025-004", "Warehouse Automation", 0, 1200000, "Automated storage and retrieval system", "Operations"),
        ("CIP-2026-001", "Quality Lab Equipment", 3, 450000, "New testing and quality control equipment", "Quality"),
    ]
    
    cost_types = [0, 1, 2, 3, 4, 5, 6, 7, 8]
    
    for proj_num, name, status, budget, desc, dept in projects:
        start_date = datetime.now() - timedelta(days=random.randint(30, 180))
        est_completion = start_date + timedelta(days=random.randint(90, 365))
        actual_completion = est_completion - timedelta(days=5) if status == 3 else None
        total_costs = int(budget * random.uniform(0.1, 0.8)) if status != 3 else budget
        
        cur.execute("""
            INSERT INTO "CipProjects" ("ProjectNumber", "Name", "Description", "Status", "StartDate", 
                "EstimatedCompletionDate", "ActualCompletionDate", "BudgetAmount", "TotalCosts", 
                "ProjectManager", "Location", "Department", "Currency", "CreatedAt")
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT ("ProjectNumber") DO NOTHING
            RETURNING "Id"
        """, (
            proj_num, name, desc, status, start_date, est_completion, actual_completion,
            budget, total_costs,
            random.choice(["Tom Anderson", "Lisa Chen", "Robert Brown"]),
            random.choice(["Building A", "Building B", "Warehouse"]),
            dept, "CAD", start_date
        ))
        
        result = cur.fetchone()
        if result:
            project_id = result[0]
            for _ in range(random.randint(5, 10)):
                cost_type = random.choice(cost_types)
                cost_type_names = ["Construction", "Engineering", "Equipment", "Labor", "Materials", "Freight", "Installation", "Testing", "Permits"]
                amount = random.randint(5000, 150000)
                cur.execute("""
                    INSERT INTO "CipCosts" ("CipProjectId", "CostType", "Description", "Amount", "Vendor",
                        "InvoiceNumber", "TransactionDate", "IsCapitalizable", "EnteredBy", "CreatedAt")
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                    ON CONFLICT DO NOTHING
                """, (
                    project_id, cost_type,
                    f"{cost_type_names[cost_type]} costs for {name[:20]}",
                    amount,
                    random.choice(["ABC Construction", "XYZ Engineering", "Tech Solutions Inc", "Industrial Supply Co"]),
                    f"INV-{random.randint(10000, 99999)}",
                    start_date + timedelta(days=random.randint(1, 60)),
                    status == 3 or random.choice([True, False]),
                    "admin",
                    datetime.now() - timedelta(days=random.randint(1, 30))
                ))
    
    print(f"  Created 5 CIP projects with cost entries")

def seed_bulk_operations(cur):
    """Seed bulk operation history"""
    print("Seeding bulk operations history...")
    
    # BulkOperationType: Transfer=0, StatusChange=1
    cur.execute('SELECT "Id" FROM "Assets" LIMIT 50')
    assets = [row[0] for row in cur.fetchall()]
    
    operations = [
        (0, "Moved 15 assets from Building A to Building B", 15, "admin", "Building B", "Manufacturing"),
        (1, "Changed 8 assets to Fully Depreciated status", 8, "accountant", None, None),
        (0, "Relocated warehouse equipment to new facility", 22, "admin", "Warehouse", "Shipping"),
        (1, "Marked 5 disposed assets from Q4 disposal", 5, "admin", None, None),
        (0, "Department reorganization asset moves", 12, "accountant", "Building A", "Assembly"),
    ]
    
    for op_type, desc, count, user, location, dept in operations:
        asset_ids_str = ",".join(str(a) for a in random.sample(assets, min(count, len(assets))))
        cur.execute("""
            INSERT INTO "BulkOperations" ("OperationType", "Description", "AssetsAffected", "ProcessedBy", 
                "OperationDate", "CreatedAt", "NewLocation", "NewDepartment", "AssetIds")
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT DO NOTHING
        """, (
            op_type, desc, count, user,
            datetime.now() - timedelta(days=random.randint(1, 60)),
            datetime.now() - timedelta(days=random.randint(1, 90)),
            location, dept, asset_ids_str
        ))
    
    print(f"  Created 5 bulk operation records")

def seed_api_keys(cur):
    """Seed demo API keys"""
    print("Seeding API keys...")
    
    demo_key = "cfa_demo_api_key_for_testing_purposes"
    key_hash = hashlib.sha256(demo_key.encode()).hexdigest()
    
    cur.execute("""
        INSERT INTO "ApiKeys" ("Name", "KeyHash", "KeyPrefix", "CreatedAt", "IsActive", "Scopes", "CreatedBy")
        VALUES (%s, %s, %s, %s, %s, %s, %s)
        ON CONFLICT DO NOTHING
    """, (
        "Demo Integration Key",
        key_hash,
        demo_key[:8],
        datetime.now() - timedelta(days=30),
        True,
        "assets:read,assets:write,journals:read",
        "admin"
    ))
    
    cur.execute("""
        INSERT INTO "ApiKeys" ("Name", "KeyHash", "KeyPrefix", "CreatedAt", "ExpiresAt", "IsActive", "Scopes", "CreatedBy")
        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
        ON CONFLICT DO NOTHING
    """, (
        "ERP Sync Key (Expired)",
        hashlib.sha256("expired_key".encode()).hexdigest(),
        "cfa_expr",
        datetime.now() - timedelta(days=90),
        datetime.now() - timedelta(days=30),
        False,
        "assets:read",
        "admin"
    ))
    
    print(f"  Created 2 API keys")

def seed_additional_audit_logs(cur):
    """Add more audit log entries for the new features"""
    print("Seeding additional audit logs...")
    
    actions = [
        ("MaintenanceEvent", "Created", "admin", "Scheduled preventative maintenance"),
        ("MaintenanceEvent", "Updated", "accountant", "Marked maintenance as completed"),
        ("InventoryList", "Created", "admin", "Created Q1-2026 inventory list"),
        ("InventoryScan", "Created", "accountant", "Scanned asset during inventory"),
        ("CipProject", "Created", "admin", "Created new CIP project"),
        ("CipCost", "Created", "accountant", "Added cost entry to CIP project"),
        ("BulkOperation", "Executed", "admin", "Bulk transferred 15 assets"),
        ("ApiKey", "Created", "admin", "Generated new API key"),
        ("Asset", "Imported", "admin", "Imported 25 assets via CSV"),
        ("Report", "Generated", "accountant", "Generated depreciation schedule report"),
    ]
    
    for entity, action, user, desc in actions:
        for _ in range(random.randint(2, 5)):
            cur.execute("""
                INSERT INTO "AuditLogs" ("EntityType", "EntityId", "Action", "Username", "Timestamp", "Description")
                VALUES (%s, %s, %s, %s, %s, %s)
            """, (
                entity, random.randint(1, 100), action, user,
                datetime.now() - timedelta(days=random.randint(1, 30), hours=random.randint(0, 23)),
                desc
            ))
    
    print(f"  Created additional audit log entries")

def main():
    print("=" * 60)
    print("CherryAI Fixed Assets - Full Demo Data Seeder")
    print("=" * 60)
    
    conn = get_connection()
    cur = conn.cursor()
    
    try:
        seed_inventory_data(cur)
        seed_maintenance_data(cur)
        seed_cip_data(cur)
        seed_bulk_operations(cur)
        seed_api_keys(cur)
        seed_additional_audit_logs(cur)
        
        conn.commit()
        print("\n" + "=" * 60)
        print("Demo data seeding complete!")
        print("=" * 60)
        
    except Exception as e:
        conn.rollback()
        print(f"\nError: {e}")
        raise
    finally:
        cur.close()
        conn.close()

if __name__ == "__main__":
    main()
