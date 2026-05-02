#!/usr/bin/env python3
"""
=============================================================================
DEPRECATED / DO NOT RUN — Use /Admin/DataImport seed pipelines instead.
=============================================================================

WARNING: This script contains DELETE operations that will destroy data!
         Tables affected: JournalLines, JournalEntries, AssetTransfers,
                         CapitalImprovements, CcaTransactions, AuditLogs

This script has been deprecated as of 2026-01-22 (Phase 1 Safety Hardening).
Use the C# seed pipelines via /Admin/DataImport for safe, idempotent seeding.

=============================================================================
"""

import os
import sys

# =============================================================================
# SAFETY GUARD: Block execution unless explicit override is provided
# =============================================================================
def check_safety_guards():
    """Abort unless explicit safety overrides are set."""
    
    # Check 1: Require explicit acknowledgment
    allow_dangerous = os.environ.get('ALLOW_DANGEROUS_SEED_SCRIPTS', '')
    if allow_dangerous != 'I_UNDERSTAND_THIS_CAN_DELETE_DATA':
        print("=" * 70)
        print("BLOCKED: This script is DEPRECATED and can DELETE DATA.")
        print("=" * 70)
        print()
        print("This script contains DELETE FROM operations on critical tables:")
        print("  - JournalLines")
        print("  - JournalEntries") 
        print("  - AssetTransfers")
        print("  - CapitalImprovements")
        print("  - CcaTransactions")
        print("  - AuditLogs")
        print()
        print("Use /Admin/DataImport in the web UI instead (safe, idempotent).")
        print()
        print("If you REALLY need to run this script, set:")
        print("  export ALLOW_DANGEROUS_SEED_SCRIPTS=I_UNDERSTAND_THIS_CAN_DELETE_DATA")
        print("  export ASPNETCORE_ENVIRONMENT=Development")
        print("=" * 70)
        sys.exit(1)
    
    # Check 2: Require LAB/Development environment
    env = os.environ.get('ASPNETCORE_ENVIRONMENT', '')
    if env.lower() not in ('development', 'lab'):
        print("=" * 70)
        print("BLOCKED: This script can only run in Development/LAB environment.")
        print(f"Current ASPNETCORE_ENVIRONMENT: {env or '(not set)'}")
        print("=" * 70)
        sys.exit(1)
    
    print("=" * 70)
    print("WARNING: Safety guards passed. Proceeding with DESTRUCTIVE operations.")
    print("=" * 70)

# Run safety check immediately on import
check_safety_guards()

import psycopg2
from psycopg2.extras import execute_values
from datetime import datetime, timedelta
from decimal import Decimal
import random
import json

# Database connection
DATABASE_URL = os.environ.get('DATABASE_URL')
if not DATABASE_URL:
    PGHOST = os.environ.get('PGHOST', 'localhost')
    PGPORT = os.environ.get('PGPORT', '5432')
    PGUSER = os.environ.get('PGUSER', 'postgres')
    PGPASSWORD = os.environ.get('PGPASSWORD', '')
    PGDATABASE = os.environ.get('PGDATABASE', 'postgres')
    DATABASE_URL = f"postgresql://{PGUSER}:{PGPASSWORD}@{PGHOST}:{PGPORT}/{PGDATABASE}"

def get_connection():
    return psycopg2.connect(DATABASE_URL, sslmode='prefer')

def clear_existing_demo_data(conn):
    """Clear existing transactional data before seeding"""
    with conn.cursor() as cur:
        print("Clearing existing demo data...")
        cur.execute('DELETE FROM "JournalLines"')
        cur.execute('DELETE FROM "JournalEntries"')
        cur.execute('DELETE FROM "AssetTransfers"')
        cur.execute('DELETE FROM "CapitalImprovements"')
        cur.execute('DELETE FROM "CcaTransactions"')
        cur.execute('DELETE FROM "AuditLogs"')
        conn.commit()
        print("Existing demo data cleared.")

def get_assets(conn):
    """Get all active assets"""
    with conn.cursor() as cur:
        cur.execute('''
            SELECT "Id", "AssetNumber", "Description", "Location", "AcquisitionCost", 
                   "AccumulatedDepreciation", "InServiceDate", "UsefulLifeMonths"
            FROM "Assets" 
            WHERE "Active" = true
            ORDER BY "Id"
        ''')
        return cur.fetchall()

def get_books(conn):
    """Get depreciation books"""
    with conn.cursor() as cur:
        cur.execute('SELECT "Id", "Name" FROM "Books"')
        return cur.fetchall()

def get_cca_classes(conn):
    """Get CCA classes"""
    with conn.cursor() as cur:
        cur.execute('SELECT "Id", "ClassNumber", "Rate" FROM "CcaClasses"')
        return cur.fetchall()

def seed_journal_entries(conn, assets, books):
    """Seed monthly depreciation journal entries for the past 12 months"""
    print("Seeding journal entries...")
    
    # Calculate depreciation amounts based on asset costs
    # Using a simplified calculation for demo purposes
    
    months = []
    base_date = datetime(2025, 1, 1)
    for i in range(12):
        months.append((base_date.year, base_date.month))
        base_date = base_date + timedelta(days=32)
        base_date = base_date.replace(day=1)
    
    journal_entries = []
    journal_lines = []
    journal_id = 1
    line_id = 1
    
    for book_id, book_name in books:
        for year, month in months:
            period = year * 100 + month
            posting_date = datetime(year, month, 28)  # End of month
            
            # Calculate total depreciation for the period
            # Use 1/12 of annual depreciation rate (~20% annual for most machinery)
            monthly_depr = sum([float(a[4]) * 0.20 / 12 for a in assets[:50]])  # Top 50 assets
            monthly_depr = round(monthly_depr, 2)
            
            batch = f"DEP-{period}"
            reference = f"AUTO-{period}-{book_name[:4].upper()}"
            source = "Depreciation Run"
            description = f"Monthly depreciation for {datetime(year, month, 1).strftime('%B %Y')} - {book_name}"
            
            journal_entries.append((
                journal_id, book_id, period, batch, reference, source,
                posting_date, datetime.utcnow(), description
            ))
            
            # Debit Depreciation Expense (6200)
            journal_lines.append((
                line_id, journal_id, 1, "6200", 
                f"Depreciation Expense - {datetime(year, month, 1).strftime('%b %Y')}",
                Decimal(str(monthly_depr)), Decimal('0.00')
            ))
            line_id += 1
            
            # Credit Accumulated Depreciation (1700)
            journal_lines.append((
                line_id, journal_id, 2, "1700",
                f"Accumulated Depreciation - {datetime(year, month, 1).strftime('%b %Y')}",
                Decimal('0.00'), Decimal(str(monthly_depr))
            ))
            line_id += 1
            
            journal_id += 1
    
    # Add some acquisition journal entries
    acquisition_dates = [
        (datetime(2025, 2, 15), "New CNC Vertical Machining Center", 485000),
        (datetime(2025, 4, 10), "Hydraulic Press Upgrade", 125000),
        (datetime(2025, 6, 22), "Robotic Welding Cell", 275000),
        (datetime(2025, 8, 5), "Coordinate Measuring Machine", 189000),
        (datetime(2025, 10, 18), "5-Axis Milling Machine", 650000),
        (datetime(2025, 12, 3), "Laser Cutting System", 320000),
    ]
    
    for acq_date, desc, cost in acquisition_dates:
        period = acq_date.year * 100 + acq_date.month
        batch = f"ACQ-{acq_date.strftime('%Y%m%d')}"
        
        for book_id, book_name in books:
            journal_entries.append((
                journal_id, book_id, period, batch, f"PO-{random.randint(100000, 999999)}",
                "Asset Acquisition", acq_date, datetime.utcnow(),
                f"Acquisition: {desc}"
            ))
            
            # Debit Fixed Assets (1500)
            journal_lines.append((
                line_id, journal_id, 1, "1500", f"Fixed Asset - {desc}",
                Decimal(str(cost)), Decimal('0.00')
            ))
            line_id += 1
            
            # Credit Accounts Payable (2100)
            journal_lines.append((
                line_id, journal_id, 2, "2100", f"AP - {desc}",
                Decimal('0.00'), Decimal(str(cost))
            ))
            line_id += 1
            
            journal_id += 1
    
    # Add disposal journal entries
    disposal_entries = [
        (datetime(2025, 3, 20), "Old Lathe Machine", 45000, 12000, -33000),  # Loss
        (datetime(2025, 7, 15), "Surplus Press", 28000, 35000, 7000),  # Gain
        (datetime(2025, 11, 8), "Obsolete Grinder", 18500, 5000, -13500),  # Loss
    ]
    
    for disp_date, desc, book_value, proceeds, gain_loss in disposal_entries:
        period = disp_date.year * 100 + disp_date.month
        batch = f"DISP-{disp_date.strftime('%Y%m%d')}"
        
        for book_id, book_name in books:
            journal_entries.append((
                journal_id, book_id, period, batch, f"DISP-{random.randint(1000, 9999)}",
                "Asset Disposal", disp_date, datetime.utcnow(),
                f"Disposal: {desc}"
            ))
            
            # Credit Fixed Assets (remove asset)
            journal_lines.append((
                line_id, journal_id, 1, "1500", f"Remove Asset - {desc}",
                Decimal('0.00'), Decimal(str(book_value + abs(gain_loss) if gain_loss < 0 else book_value - gain_loss))
            ))
            line_id += 1
            
            # Debit Cash/Receivable for proceeds
            journal_lines.append((
                line_id, journal_id, 2, "1000", f"Proceeds - {desc}",
                Decimal(str(proceeds)), Decimal('0.00')
            ))
            line_id += 1
            
            # Debit Accumulated Depreciation
            accum_depr = book_value + abs(gain_loss) if gain_loss < 0 else book_value - gain_loss
            accum_depr = max(0, accum_depr - proceeds)
            journal_lines.append((
                line_id, journal_id, 3, "1700", f"Release Accum Depr - {desc}",
                Decimal(str(accum_depr)), Decimal('0.00')
            ))
            line_id += 1
            
            if gain_loss > 0:
                # Credit Gain on Disposal (4500)
                journal_lines.append((
                    line_id, journal_id, 4, "4500", f"Gain on Disposal - {desc}",
                    Decimal('0.00'), Decimal(str(gain_loss))
                ))
            else:
                # Debit Loss on Disposal (6800)
                journal_lines.append((
                    line_id, journal_id, 4, "6800", f"Loss on Disposal - {desc}",
                    Decimal(str(abs(gain_loss))), Decimal('0.00')
                ))
            line_id += 1
            
            journal_id += 1
    
    # Insert journal entries
    with conn.cursor() as cur:
        execute_values(cur, '''
            INSERT INTO "JournalEntries" ("Id", "BookId", "Period", "Batch", "Reference", 
                                          "Source", "PostingDate", "CreatedUtc", "Description")
            VALUES %s
        ''', journal_entries)
        
        execute_values(cur, '''
            INSERT INTO "JournalLines" ("Id", "JournalEntryId", "LineNo", "Account", 
                                        "Description", "Debit", "Credit")
            VALUES %s
        ''', journal_lines)
        
        # Reset sequences
        cur.execute(f'SELECT setval(\'"JournalEntries_Id_seq"\', {journal_id})')
        cur.execute(f'SELECT setval(\'"JournalLines_Id_seq"\', {line_id})')
        
    conn.commit()
    print(f"Created {len(journal_entries)} journal entries with {len(journal_lines)} lines.")

def seed_asset_transfers(conn, assets):
    """Seed asset transfer history"""
    print("Seeding asset transfers...")
    
    locations = ["MISS", "BURL 1", "BURL 2", "DUNDALK", "BRAM", "John Lucas", "1601"]
    departments = ["Production", "Assembly", "Machining", "Quality Control", "Maintenance", "R&D"]
    reasons = [
        "Production line reorganization",
        "Capacity expansion",
        "Equipment consolidation",
        "New facility setup",
        "Maintenance requirements",
        "Efficiency improvement"
    ]
    users = ["jsmith", "mwilson", "kbrown", "dlee", "admin"]
    
    transfers = []
    transfer_id = 1
    
    # Generate transfers throughout the year
    for month in range(1, 13):
        # 3-5 transfers per month
        num_transfers = random.randint(3, 5)
        
        for _ in range(num_transfers):
            asset = random.choice(assets)
            from_loc = asset[3] if asset[3] else random.choice(locations)
            to_loc = random.choice([l for l in locations if l != from_loc])
            from_dept = random.choice(departments)
            to_dept = random.choice(departments)
            
            transfer_date = datetime(2025, month, random.randint(1, 28))
            
            transfers.append((
                transfer_id,
                asset[0],  # AssetId
                transfer_date,
                from_loc,
                f"Bay {random.randint(1, 20)}",
                from_dept,
                to_loc,
                f"Bay {random.randint(1, 20)}",
                to_dept,
                random.choice(reasons),
                f"Transfer approved by operations manager. Asset relocated successfully.",
                datetime.utcnow(),
                random.choice(users)
            ))
            transfer_id += 1
    
    with conn.cursor() as cur:
        execute_values(cur, '''
            INSERT INTO "AssetTransfers" ("Id", "AssetId", "TransferDate", "FromLocation", 
                                          "FromBay", "FromDepartment", "ToLocation", "ToBay",
                                          "ToDepartment", "Reason", "Notes", "CreatedAt", "CreatedBy")
            VALUES %s
        ''', transfers)
        cur.execute(f'SELECT setval(\'"AssetTransfers_Id_seq"\', {transfer_id})')
    
    conn.commit()
    print(f"Created {len(transfers)} asset transfers.")

def seed_capital_improvements(conn, assets):
    """Seed capital improvement records"""
    print("Seeding capital improvements...")
    
    improvement_types = [
        ("CNC Control Upgrade", 45000, 85000, 24),
        ("Spindle Replacement", 25000, 55000, 18),
        ("Axis Motor Upgrade", 15000, 35000, 12),
        ("Coolant System Upgrade", 8000, 18000, 0),
        ("Safety Guard Installation", 5000, 12000, 0),
        ("Precision Calibration Kit", 12000, 28000, 12),
        ("Automation Module", 55000, 120000, 36),
        ("Laser Measurement System", 22000, 45000, 18),
        ("Tool Changer Upgrade", 18000, 38000, 12),
        ("Hydraulic System Overhaul", 20000, 45000, 24),
    ]
    
    vendors = [
        "Siemens Industrial Services",
        "FANUC America Corporation",
        "Mazak Service Division",
        "DMG MORI Service",
        "Haas Automation",
        "Precision Tooling Inc.",
        "Advanced Automation Systems",
        "Industrial Controls Ltd."
    ]
    
    users = ["jsmith", "mwilson", "kbrown", "dlee", "admin"]
    
    improvements = []
    improvement_id = 1
    
    # High-value assets get improvements
    high_value_assets = [a for a in assets if float(a[4]) > 500000][:30]
    
    for month in range(1, 13):
        # 2-4 improvements per month
        num_improvements = random.randint(2, 4)
        
        for _ in range(num_improvements):
            asset = random.choice(high_value_assets)
            imp_type, min_cost, max_cost, life_ext = random.choice(improvement_types)
            cost = round(random.uniform(min_cost, max_cost), 2)
            
            improvement_date = datetime(2025, month, random.randint(1, 28))
            
            improvements.append((
                improvement_id,
                asset[0],  # AssetId
                improvement_date,
                f"{imp_type} - {asset[2][:30]}",  # Description with asset name
                Decimal(str(cost)),
                random.choice(vendors),
                f"INV-{random.randint(100000, 999999)}",
                life_ext if life_ext > 0 else None,
                f"Improvement completed successfully. Asset performance improved by {random.randint(10, 30)}%.",
                True,  # Capitalized
                datetime.utcnow(),
                random.choice(users)
            ))
            improvement_id += 1
    
    with conn.cursor() as cur:
        execute_values(cur, '''
            INSERT INTO "CapitalImprovements" ("Id", "AssetId", "ImprovementDate", "Description",
                                               "Cost", "Vendor", "InvoiceNumber", 
                                               "UsefulLifeExtensionMonths", "Notes", "Capitalized",
                                               "CreatedAt", "CreatedBy")
            VALUES %s
        ''', improvements)
        cur.execute(f'SELECT setval(\'"CapitalImprovements_Id_seq"\', {improvement_id})')
    
    conn.commit()
    print(f"Created {len(improvements)} capital improvements.")

def seed_cca_transactions(conn, assets, cca_classes):
    """Seed CCA tax transactions for Canadian tax compliance"""
    print("Seeding CCA transactions...")
    
    transactions = []
    trans_id = 1
    
    users = ["admin", "kbrown", "taxmanager"]
    
    # Assign assets to CCA classes (Class 8 for machinery is most common)
    class_8_id = None
    class_43_id = None
    class_10_id = None
    
    for cca_id, class_num, rate in cca_classes:
        if class_num == 8:
            class_8_id = cca_id
        elif class_num == 43:
            class_43_id = cca_id
        elif class_num == 10:
            class_10_id = cca_id
    
    if not class_8_id:
        class_8_id = cca_classes[4][0]  # Default fallback
    
    # Transaction types: 0=Addition, 1=Disposal, 2=CCADeduction, 3=Adjustment
    
    # Add acquisitions throughout the year
    acquisition_months = [2, 4, 6, 8, 10, 12]
    
    for month in acquisition_months:
        num_acq = random.randint(2, 4)
        assets_sample = random.sample(assets[:100], min(num_acq, len(assets[:100])))
        
        for asset in assets_sample:
            trans_date = datetime(2025, month, random.randint(1, 28))
            capital_cost = float(asset[4]) * 0.1  # 10% of original for simulation
            
            transactions.append((
                trans_id,
                class_8_id,
                asset[0],
                2025,
                0,  # Addition
                trans_date,
                trans_date + timedelta(days=random.randint(30, 90)),  # Available for use
                Decimal(str(round(capital_cost, 2))),
                None,  # Proceeds
                Decimal(str(round(capital_cost, 2))),  # ACB
                Decimal(str(round(capital_cost, 2))),  # Net Addition
                True,  # Subject to half-year
                random.choice([True, False]),  # AII eligible
                f"Capital acquisition - {asset[2][:40]}",
                f"Added via asset import. CCA Class 8 @ 20%.",
                datetime.utcnow(),
                random.choice(users)
            ))
            trans_id += 1
    
    # Add quarterly CCA deductions
    for quarter in [3, 6, 9, 12]:
        trans_date = datetime(2025, quarter, 28)
        
        # Calculate quarterly CCA claim (simplified)
        quarterly_cca = round(random.uniform(800000, 1200000), 2)
        
        transactions.append((
            trans_id,
            class_8_id,
            None,  # No specific asset
            2025,
            2,  # CCA Deduction
            trans_date,
            None,
            Decimal('0.00'),
            None,
            Decimal('0.00'),
            Decimal(str(-quarterly_cca)),  # Negative for deduction
            False,
            False,
            f"Q{quarter//3} 2025 CCA Claim - Class 8",
            f"Quarterly CCA deduction calculated at 20% declining balance.",
            datetime.utcnow(),
            "admin"
        ))
        trans_id += 1
    
    # Add a few disposals
    disposal_months = [3, 7, 11]
    for month in disposal_months:
        asset = random.choice(assets[:50])
        trans_date = datetime(2025, month, random.randint(15, 28))
        proceeds = round(float(asset[4]) * random.uniform(0.1, 0.3), 2)
        
        transactions.append((
            trans_id,
            class_8_id,
            asset[0],
            2025,
            1,  # Disposal
            trans_date,
            None,
            Decimal('0.00'),
            Decimal(str(proceeds)),
            Decimal(str(-proceeds)),  # Reduce ACB
            Decimal(str(-proceeds)),
            False,
            False,
            f"Asset disposal - {asset[2][:40]}",
            f"Proceeds received. Check for recapture/terminal loss.",
            datetime.utcnow(),
            random.choice(users)
        ))
        trans_id += 1
    
    with conn.cursor() as cur:
        execute_values(cur, '''
            INSERT INTO "CcaTransactions" ("Id", "CcaClassId", "AssetId", "FiscalYear",
                                           "TransactionType", "TransactionDate", 
                                           "AvailableForUseDate", "CapitalCost", "Proceeds",
                                           "AdjustedCostBase", "NetAddition", 
                                           "SubjectToHalfYearRule", "IsAcceleratedIncentiveEligible",
                                           "Description", "Notes", "CreatedAt", "CreatedBy")
            VALUES %s
        ''', transactions)
        cur.execute(f'SELECT setval(\'"CcaTransactions_Id_seq"\', {trans_id})')
    
    conn.commit()
    print(f"Created {len(transactions)} CCA transactions.")

def seed_audit_logs(conn, assets):
    """Seed audit trail entries"""
    print("Seeding audit logs...")
    
    actions = ["Create", "Update", "Delete", "View", "Export", "Import", "Approve", "Reject"]
    entity_types = ["Asset", "JournalEntry", "Book", "Transfer", "Improvement", "CcaTransaction"]
    users = ["admin", "jsmith", "mwilson", "kbrown", "dlee", "accountant", "viewer"]
    ip_addresses = ["192.168.1.100", "192.168.1.101", "10.0.0.50", "172.16.0.25", "192.168.1.105"]
    
    descriptions = [
        "Viewed asset details",
        "Updated asset location",
        "Modified depreciation settings",
        "Generated depreciation report",
        "Exported asset list to Excel",
        "Approved transfer request",
        "Added capital improvement",
        "Updated CCA class assignment",
        "Reviewed journal entries",
        "Posted depreciation batch",
        "Modified GL account mapping",
        "Updated book settings",
        "Imported assets from Excel",
        "Ran depreciation preview",
        "Generated CCA schedule",
    ]
    
    logs = []
    log_id = 1
    
    # Generate logs throughout the year
    for month in range(1, 13):
        # 20-40 audit entries per month
        num_logs = random.randint(20, 40)
        
        for _ in range(num_logs):
            asset = random.choice(assets) if random.random() > 0.3 else None
            entity_type = random.choice(entity_types)
            action = random.choice(actions)
            timestamp = datetime(2025, month, random.randint(1, 28), 
                                random.randint(8, 18), random.randint(0, 59))
            
            # Create before/after JSON for updates
            before_json = None
            after_json = None
            if action == "Update" and asset:
                before_json = json.dumps({
                    "Location": asset[3],
                    "Status": "Active"
                })
                after_json = json.dumps({
                    "Location": random.choice(["MISS", "BURL 1", "DUNDALK"]),
                    "Status": "Active"
                })
            
            logs.append((
                log_id,
                entity_type,
                asset[0] if asset else random.randint(1, 100),
                action,
                before_json,
                after_json,
                random.choice(users),
                timestamp,
                random.choice(ip_addresses),
                random.choice(descriptions)
            ))
            log_id += 1
    
    with conn.cursor() as cur:
        execute_values(cur, '''
            INSERT INTO "AuditLogs" ("Id", "EntityType", "EntityId", "Action",
                                     "BeforeJson", "AfterJson", "Username", 
                                     "Timestamp", "IpAddress", "Description")
            VALUES %s
        ''', logs)
        cur.execute(f'SELECT setval(\'"AuditLogs_Id_seq"\', {log_id})')
    
    conn.commit()
    print(f"Created {len(logs)} audit log entries.")

def seed_cca_class_balances(conn, cca_classes):
    """Seed CCA Class Balances for 2025 and 2026 fiscal years"""
    print("Seeding CCA class balances...")
    
    balances = []
    balance_id = 1
    
    # Generate balances for 2025 and 2026
    for year in [2025, 2026]:
        for cca_id, class_num, rate in cca_classes:
            rate_float = float(rate)
            
            # Generate realistic UCC figures based on class type
            if class_num == 8:  # Machinery - largest class
                opening_ucc = round(random.uniform(8000000, 12000000), 2)
                additions = round(random.uniform(500000, 1500000), 2)
                dispositions = round(random.uniform(50000, 200000), 2)
            elif class_num in [43, 46, 50]:  # Manufacturing equipment
                opening_ucc = round(random.uniform(2000000, 5000000), 2)
                additions = round(random.uniform(200000, 600000), 2)
                dispositions = round(random.uniform(20000, 80000), 2)
            elif class_num in [1, 3]:  # Buildings
                opening_ucc = round(random.uniform(3000000, 8000000), 2)
                additions = round(random.uniform(100000, 500000), 2)
                dispositions = round(random.uniform(0, 50000), 2)
            elif class_num == 10:  # Vehicles
                opening_ucc = round(random.uniform(200000, 600000), 2)
                additions = round(random.uniform(50000, 150000), 2)
                dispositions = round(random.uniform(10000, 40000), 2)
            elif class_num == 12:  # 100% - small tools
                opening_ucc = round(random.uniform(50000, 150000), 2)
                additions = round(random.uniform(20000, 60000), 2)
                dispositions = round(random.uniform(0, 10000), 2)
            else:
                opening_ucc = round(random.uniform(100000, 500000), 2)
                additions = round(random.uniform(20000, 100000), 2)
                dispositions = round(random.uniform(5000, 30000), 2)
            
            # Apply 2026 carryforward from 2025 closing
            if year == 2026:
                opening_ucc = round(opening_ucc * 0.8, 2)  # Reduced by prior year CCA
            
            # Calculate half-year adjustment (half of net additions subject to half-year rule)
            net_additions = additions - dispositions
            half_year_adj = round(net_additions * 0.5, 2) if net_additions > 0 else Decimal('0.00')
            
            # Base for CCA = Opening + Additions - Dispositions - Half Year Adjustment
            base_for_cca = opening_ucc + additions - dispositions - float(half_year_adj)
            base_for_cca = max(0, base_for_cca)
            
            # CCA Claimed = Base * Rate
            cca_claimed = round(base_for_cca * rate_float, 2)
            
            # Closing UCC = Base - CCA Claimed + Half Year Adjustment carried back
            closing_ucc = round(base_for_cca - cca_claimed, 2)
            
            balances.append((
                balance_id,
                cca_id,
                year,
                Decimal(str(opening_ucc)),
                Decimal(str(additions)),
                Decimal(str(dispositions)),
                half_year_adj,
                Decimal(str(round(base_for_cca, 2))),
                Decimal(str(cca_claimed)),
                Decimal(str(closing_ucc)),
                None,  # Recapture
                None,  # Terminal Loss
                True,  # IsPosted
                datetime(year, 12, 31),  # PostedDate
                "admin",  # PostedBy
                365,  # DaysInFiscalPeriod
                False  # IsShortFiscalPeriod
            ))
            balance_id += 1
    
    with conn.cursor() as cur:
        # Clear existing balances
        cur.execute('DELETE FROM "CcaClassBalances"')
        
        execute_values(cur, '''
            INSERT INTO "CcaClassBalances" ("Id", "CcaClassId", "FiscalYear", "OpeningUcc",
                                            "Additions", "Dispositions", "HalfYearAdjustment",
                                            "BaseForCca", "CcaClaimed", "ClosingUcc", "Recapture",
                                            "TerminalLoss", "IsPosted", "PostedDate", "PostedBy",
                                            "DaysInFiscalPeriod", "IsShortFiscalPeriod")
            VALUES %s
        ''', balances)
        cur.execute(f'SELECT setval(\'"CcaClassBalances_Id_seq"\', {balance_id})')
    
    conn.commit()
    print(f"Created {len(balances)} CCA class balance records.")

def update_asset_accumulated_depreciation(conn, assets):
    """Update accumulated depreciation on assets based on journal entries"""
    print("Updating accumulated depreciation on assets...")
    
    # Calculate reasonable accumulated depreciation based on age and cost
    with conn.cursor() as cur:
        for asset in assets:
            asset_id = asset[0]
            cost = float(asset[4])
            in_service = asset[6]
            
            # Calculate years since in service
            if in_service:
                years = (datetime.now() - in_service.replace(tzinfo=None)).days / 365.25
            else:
                years = 10  # Default
            
            # Use declining balance at 20% with max 90% depreciated
            remaining = cost * (0.8 ** min(years, 15))
            accum_depr = min(cost * 0.9, cost - remaining)
            accum_depr = round(accum_depr, 2)
            
            # Calculate book value
            book_value = round(cost - accum_depr, 2)
            
            # Calculate fair market value (random factor based on condition)
            fmv_factor = random.uniform(0.6, 1.2)
            fmv = round(book_value * fmv_factor, 2)
            
            cur.execute('''
                UPDATE "Assets" 
                SET "AccumulatedDepreciation" = %s,
                    "BookValue" = %s,
                    "FairMarketValue" = %s,
                    "LastDepreciationDate" = %s
                WHERE "Id" = %s
            ''', (Decimal(str(accum_depr)), Decimal(str(book_value)), 
                  Decimal(str(fmv)), datetime(2025, 12, 31), asset_id))
    
    conn.commit()
    print("Updated accumulated depreciation on all assets.")

def main():
    print("=" * 60)
    print("CherryAI Fixed Assets - Demo Data Seeder")
    print("=" * 60)
    
    conn = get_connection()
    
    try:
        # Clear existing demo data
        clear_existing_demo_data(conn)
        
        # Get reference data
        assets = get_assets(conn)
        books = get_books(conn)
        cca_classes = get_cca_classes(conn)
        
        print(f"\nFound {len(assets)} active assets")
        print(f"Found {len(books)} books")
        print(f"Found {len(cca_classes)} CCA classes")
        print()
        
        # Seed all transactional data
        seed_journal_entries(conn, assets, books)
        seed_asset_transfers(conn, assets)
        seed_capital_improvements(conn, assets)
        seed_cca_transactions(conn, assets, cca_classes)
        seed_cca_class_balances(conn, cca_classes)
        seed_audit_logs(conn, assets)
        update_asset_accumulated_depreciation(conn, assets)
        
        print("\n" + "=" * 60)
        print("Demo data seeding complete!")
        print("=" * 60)
        
        # Print summary
        with conn.cursor() as cur:
            cur.execute('SELECT COUNT(*) FROM "JournalEntries"')
            je_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "JournalLines"')
            jl_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "AssetTransfers"')
            at_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "CapitalImprovements"')
            ci_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "CcaTransactions"')
            cca_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "CcaClassBalances"')
            ccab_count = cur.fetchone()[0]
            cur.execute('SELECT COUNT(*) FROM "AuditLogs"')
            al_count = cur.fetchone()[0]
        
        print(f"\nSummary:")
        print(f"  - Journal Entries: {je_count}")
        print(f"  - Journal Lines: {jl_count}")
        print(f"  - Asset Transfers: {at_count}")
        print(f"  - Capital Improvements: {ci_count}")
        print(f"  - CCA Transactions: {cca_count}")
        print(f"  - CCA Class Balances: {ccab_count}")
        print(f"  - Audit Logs: {al_count}")
        
    finally:
        conn.close()

if __name__ == "__main__":
    main()
