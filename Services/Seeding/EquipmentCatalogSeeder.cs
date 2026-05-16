using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    // Sprint 2 PR #117.2 — Equipment Catalog seeder.
    //
    // Per Dean: "Best in Class Process to Produce a Best In Class product."
    // The curated source of truth is EQUIPMENT_CATALOG.md (reviewable, version-
    // controlled, edit-friendly). This seeder transcribes that catalog into
    // the EquipmentClasses + EquipmentModels + SensorProfiles tables on app
    // startup, so downstream code (IndustrialAssetSeeder, Plant Floor, the
    // asset detail view) reads structured rows instead of hardcoded C# arrays.
    //
    // Idempotent: if EquipmentClasses already has rows it bails. forceReseed
    // is provided for the admin-endpoint reset path.
    //
    // Data sourced from EQUIPMENT_CATALOG.md (14 classes, real Mfr/Model
    // pairs, sensor profiles grounded in ISO 10816-3 / AWS A5.18 / ANSI B11.1
    // and OEM published service docs).
    public interface IEquipmentCatalogSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class EquipmentCatalogSeeder : IEquipmentCatalogSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<EquipmentCatalogSeeder> _logger;

        public EquipmentCatalogSeeder(AppDbContext db, ILogger<EquipmentCatalogSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            var existing = await _db.EquipmentClasses.CountAsync();
            if (existing > 0 && !forceReseed)
            {
                _logger.LogInformation("EquipmentCatalogSeeder: skipping, {Count} classes already present.", existing);
                return 0;
            }

            if (forceReseed && existing > 0)
            {
                _logger.LogWarning("EquipmentCatalogSeeder: forceReseed=true; wiping catalog tables.");
                _db.SensorProfiles.RemoveRange(_db.SensorProfiles);
                _db.EquipmentModels.RemoveRange(_db.EquipmentModels);
                _db.EquipmentClasses.RemoveRange(_db.EquipmentClasses);
                await _db.SaveChangesAsync();
            }

            var classes = BuildCatalog();
            _db.EquipmentClasses.AddRange(classes);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "EquipmentCatalogSeeder: inserted {Classes} classes, {Models} models, {Sensors} sensor profiles.",
                classes.Count,
                classes.Sum(c => c.Models.Count),
                classes.Sum(c => c.SensorProfiles.Count));

            return classes.Count;
        }

        // -------------------------------------------------------------------
        // CATALOG DATA — mirrors EQUIPMENT_CATALOG.md (Sections 1-14).
        // Edit-friendly: each class is one method returning a fully-populated
        // EquipmentClass with its Models and SensorProfiles nested. New classes
        // just go in BuildCatalog() — no schema change required.
        // -------------------------------------------------------------------
        private static List<EquipmentClass> BuildCatalog() => new()
        {
            CncMachiningCenter(),
            CncLathe(),
            CncFiveAxis(),
            WeldingRobot(),
            MaterialHandlingRobot(),
            WeldingPowerSource(),
            StampingPress(),
            PressBrake(),
            LaserCutter(),
            IndustrialConveyor(),
            AirCompressor(),
            Forklift(),
            HvacUnit(),
            Cmm(),
        };

        // ---- 1. CNC Machining Center ----
        private static EquipmentClass CncMachiningCenter() => new()
        {
            Code = "CNC_MACHINING_CENTER",
            Name = "CNC Machining Center",
            Category = "Machining",
            IconCode = "milling",
            Description = "Vertical and horizontal CNC machining centers used for precision toolroom work and tier-1 production milling.",
            DisplayOrder = 10,
            Models =
            {
                Model("Haas Automation, Inc.", "VF-2SS", "Haas VF-2SS",
                    "https://www.haascnc.com/machines/vertical-mills/vf-series/models/vf-2ss.html",
                    "/assets/equipment/cnc/haas-vf2ss.jpg",
                    "https://www.haascnc.com/service/operators-manuals.html",
                    cost: 72_000m, life: 18, weight: 5,
                    notes: "Workhorse VMC in tooling/repair shops; Haas publishes a full operator manual as a free PDF."),
                Model("Mazak Corporation", "VARIAXIS i-700", "Mazak VARIAXIS i-700",
                    "https://www.mazakusa.com/machines/variaxis-i-700/",
                    "/assets/equipment/cnc/mazak-variaxis-i700.jpg",
                    "https://www.mazakusa.com/support/",
                    cost: 385_000m, life: 20, weight: 2),
                Model("Okuma America Corporation", "GENOS M460-VE", "Okuma GENOS M460-VE",
                    "https://www.okuma.com/genos-m460-ve",
                    "/assets/equipment/cnc/okuma-m460ve.jpg",
                    "https://www.okuma.com/support",
                    cost: 145_000m, life: 18, weight: 2),
                Model("DMG MORI USA", "DMU 50", "DMG MORI DMU 50",
                    "https://us.dmgmori.com/products/machines/milling/5-axis-milling/dmu/dmu-50",
                    "/assets/equipment/cnc/dmgmori-dmu50.jpg",
                    "https://us.dmgmori.com/service-and-customer-care",
                    cost: 215_000m, life: 18, weight: 2),
                Model("Makino Inc.", "PS65", "Makino PS65",
                    "https://www.makino.com/en-us/machines/vertical-machining-centers/ps65",
                    "/assets/equipment/cnc/makino-ps65.jpg",
                    "https://www.makino.com/en-us/service",
                    cost: 165_000m, life: 18, weight: 2),
            },
            SensorProfiles =
            {
                Sensor("Spindle Temp", SensorReadingType.Temperature, "°C", 30m, 65m, 70m, 75m, 60, isPrimary: true, order: 10,
                    notes: "Bearing temperature on spindle housing; gradual rise > baseline + 5°C across a 30-day window is the classic bearing-failure signature."),
                Sensor("Spindle Vibration", SensorReadingType.Vibration, "mm/s", 0.5m, 2.5m, 3.5m, 4.5m, 60, isPrimary: true, order: 20,
                    notes: "ISO 10816-3 Class C boundary at 4.5 mm/s RMS — beyond this, bearing replacement is imminent."),
                Sensor("Spindle Load", SensorReadingType.Load, "%", 20m, 80m, 90m, 95m, 60, isPrimary: true, order: 30,
                    notes: "Sustained load > 95% nameplate for > 30 s indicates over-aggressive tool path or worn tool."),
                Sensor("Coolant Pressure", SensorReadingType.Pressure, "bar", 4m, 12m, 3m, 2m, 60, isPrimary: false, order: 40,
                    breachHigh: false,
                    notes: "Low-side breach: coolant pressure drop = pump degradation."),
                Sensor("Hydraulic Oil Temp", SensorReadingType.Temperature, "°C", 35m, 55m, 60m, 65m, 60, isPrimary: false, order: 50),
                Sensor("Spindle Hours", SensorReadingType.Hours, "hr", 0m, 50_000m, null, null, 60, isPrimary: false, order: 60,
                    notes: "Cumulative spindle running hours — drives PM cadence."),
            }
        };

        // ---- 2. CNC Lathe ----
        private static EquipmentClass CncLathe() => new()
        {
            Code = "CNC_LATHE",
            Name = "CNC Lathe / Turning Center",
            Category = "Machining",
            IconCode = "turning",
            Description = "CNC turning centers for high-volume cylindrical parts and shafts.",
            DisplayOrder = 20,
            Models =
            {
                Model("Mazak Corporation", "QUICK TURN 250", "Mazak QUICK TURN 250",
                    "https://www.mazakusa.com/machines/quick-turn-250/",
                    "/assets/equipment/lathe/mazak-qt250.jpg", null,
                    cost: 165_000m, life: 18, weight: 3),
                Model("Okuma America Corporation", "LB3000 EX III", "Okuma LB3000 EX III",
                    "https://www.okuma.com/lb3000-ex-iii",
                    "/assets/equipment/lathe/okuma-lb3000.jpg", null,
                    cost: 195_000m, life: 18, weight: 2),
                Model("Haas Automation, Inc.", "ST-30Y", "Haas ST-30Y",
                    "https://www.haascnc.com/machines/lathes/st-series/models/st-30y.html",
                    "/assets/equipment/lathe/haas-st30y.jpg",
                    "https://www.haascnc.com/service/operators-manuals.html",
                    cost: 89_000m, life: 18, weight: 4),
                Model("DN Solutions (Doosan)", "PUMA 2600", "DN Solutions PUMA 2600",
                    "https://www.dnsolutions.com/products/cnc-lathes/puma-2600",
                    "/assets/equipment/lathe/doosan-puma2600.jpg", null,
                    cost: 110_000m, life: 18, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Spindle Temp", SensorReadingType.Temperature, "°C", 30m, 60m, 65m, 70m, 60, isPrimary: true, order: 10),
                Sensor("Spindle Vibration", SensorReadingType.Vibration, "mm/s", 0.4m, 2.0m, 3.5m, 4.5m, 60, isPrimary: true, order: 20),
                Sensor("Chuck Pressure", SensorReadingType.Pressure, "bar", 15m, 35m, 13m, 12m, 60, isPrimary: true, order: 30,
                    breachHigh: false,
                    notes: "Low-side: drop in clamping pressure risks workpiece slip during high-speed turning."),
                Sensor("Spindle Load", SensorReadingType.Load, "%", 25m, 85m, 95m, 100m, 60, isPrimary: false, order: 40),
                Sensor("Coolant Flow", SensorReadingType.Flow, "L/min", 20m, 80m, 17m, 15m, 60, isPrimary: false, order: 50,
                    breachHigh: false),
            }
        };

        // ---- 3. 5-Axis CNC ----
        private static EquipmentClass CncFiveAxis() => new()
        {
            Code = "CNC_5AXIS",
            Name = "5-Axis CNC Machining Center",
            Category = "Machining",
            IconCode = "five-axis",
            Description = "Simultaneous 5-axis machining centers for aerospace, mold, and complex tier-1 components.",
            DisplayOrder = 30,
            Models =
            {
                Model("Mazak Corporation", "VARIAXIS C-600", "Mazak VARIAXIS C-600",
                    "https://www.mazakusa.com/machines/variaxis-c-600/",
                    "/assets/equipment/5axis/mazak-variaxis-c600.jpg", null,
                    cost: 485_000m, life: 20, weight: 2),
                Model("DMG MORI USA", "NHX 5000", "DMG MORI NHX 5000",
                    "https://us.dmgmori.com/products/machines/milling/horizontal-machining/nhx/nhx-5000",
                    "/assets/equipment/5axis/dmgmori-nhx5000.jpg", null,
                    cost: 545_000m, life: 20, weight: 2),
                Model("Makino Inc.", "a51nx", "Makino a51nx",
                    "https://www.makino.com/en-us/machines/horizontal-machining-centers/a51nx",
                    "/assets/equipment/5axis/makino-a51nx.jpg", null,
                    cost: 625_000m, life: 22, weight: 1),
            },
            SensorProfiles =
            {
                Sensor("Spindle Temp", SensorReadingType.Temperature, "°C", 30m, 60m, 65m, 70m, 60, isPrimary: true, order: 10),
                Sensor("Spindle Vibration", SensorReadingType.Vibration, "mm/s", 0.3m, 1.8m, 3.0m, 4.5m, 60, isPrimary: true, order: 20),
                Sensor("Rotary Axis Temp", SensorReadingType.Temperature, "°C", 25m, 50m, 60m, 70m, 60, isPrimary: true, order: 30,
                    notes: "Trunnion/rotary axis bearing temp — critical for 5-axis position accuracy."),
                Sensor("Coolant Pressure", SensorReadingType.Pressure, "bar", 4m, 12m, 3m, 2m, 60, isPrimary: false, order: 40, breachHigh: false),
                Sensor("Spindle Load", SensorReadingType.Load, "%", 20m, 80m, 90m, 95m, 60, isPrimary: false, order: 50),
            }
        };

        // ---- 4. Welding Robot ----
        private static EquipmentClass WeldingRobot() => new()
        {
            Code = "WELDING_ROBOT",
            Name = "Welding Robot",
            Category = "Welding",
            IconCode = "welding-robot",
            Description = "Articulated industrial robots configured for MIG/MAG arc welding in stamping and body-in-white cells.",
            DisplayOrder = 40,
            Models =
            {
                Model("FANUC America Corporation", "ARC Mate 100iD", "FANUC ARC Mate 100iD",
                    "https://www.fanucamerica.com/products/robots/series/arc-mate/arc-mate-100id",
                    "/assets/equipment/robot/fanuc-arcmate100id.jpg", null,
                    cost: 65_000m, life: 15, weight: 4,
                    notes: "Most common arc welding cell robot in North American automotive."),
                Model("KUKA Robotics Corporation", "KR 16-2 arc HW", "KUKA KR 16-2 arc HW",
                    "https://www.kuka.com/en-us/products/robotics-systems/industrial-robots/kr-cybertech",
                    "/assets/equipment/robot/kuka-kr16-2.jpg", null,
                    cost: 58_000m, life: 15, weight: 3),
                Model("ABB Robotics", "IRB 1660ID", "ABB IRB 1660ID",
                    "https://new.abb.com/products/robotics/industrial-robots/irb-1660id",
                    "/assets/equipment/robot/abb-irb1660id.jpg", null,
                    cost: 62_000m, life: 15, weight: 3),
                Model("Yaskawa Motoman", "MA-2010", "Yaskawa Motoman MA-2010",
                    "https://www.motoman.com/en-us/products/robots/industrial/arc-welding/ma2010",
                    "/assets/equipment/robot/yaskawa-ma2010.jpg", null,
                    cost: 55_000m, life: 15, weight: 3),
                Model("KUKA Robotics Corporation", "KR 210 R2700", "KUKA KR 210 R2700",
                    "https://www.kuka.com/en-us/products/robotics-systems/industrial-robots/kr-quantec",
                    "/assets/equipment/robot/kuka-kr210.jpg", null,
                    cost: 95_000m, life: 15, weight: 2,
                    notes: "Heavy-payload robot used for body-in-white spot/arc tandems. Storyline asset for PR #117.2 servo-overheat scenario."),
            },
            SensorProfiles =
            {
                Sensor("Axis-3 Motor Temp", SensorReadingType.Temperature, "°C", 30m, 65m, 75m, 85m, 60, isPrimary: true, order: 10,
                    notes: "Servo motor windings; rising on repeat duty cycle is a textbook servo-drive fatigue pattern."),
                Sensor("Axis-3 Servo Current", SensorReadingType.Current, "A", 5m, 28m, 32m, 36m, 60, isPrimary: true, order: 20),
                Sensor("Controller Temp", SensorReadingType.Temperature, "°C", 25m, 45m, 55m, 65m, 60, isPrimary: false, order: 30),
                Sensor("Joint Vibration", SensorReadingType.Vibration, "mm/s", 0.2m, 1.5m, 2.5m, 3.5m, 60, isPrimary: false, order: 40),
                Sensor("Duty Cycle", SensorReadingType.DutyCycle, "%", 30m, 80m, 90m, 95m, 60, isPrimary: true, order: 50),
            }
        };

        // ---- 5. Material-Handling Robot ----
        private static EquipmentClass MaterialHandlingRobot() => new()
        {
            Code = "MATERIAL_HANDLING_ROBOT",
            Name = "Material-Handling Robot",
            Category = "Material Handling",
            IconCode = "robot-pick",
            Description = "Pick-and-place robots for press tending, dunnage, and machine loading.",
            DisplayOrder = 50,
            Models =
            {
                Model("FANUC America Corporation", "M-710iC/50", "FANUC M-710iC/50",
                    "https://www.fanucamerica.com/products/robots/series/m-710/m-710ic-50",
                    "/assets/equipment/robot/fanuc-m710ic50.jpg", null,
                    cost: 78_000m, life: 15, weight: 3),
                Model("ABB Robotics", "IRB 6700-200/2.60", "ABB IRB 6700",
                    "https://new.abb.com/products/robotics/industrial-robots/irb-6700",
                    "/assets/equipment/robot/abb-irb6700.jpg", null,
                    cost: 115_000m, life: 15, weight: 2),
                Model("Yaskawa Motoman", "GP180", "Yaskawa Motoman GP180",
                    "https://www.motoman.com/en-us/products/robots/industrial/material-handling/gp180",
                    "/assets/equipment/robot/yaskawa-gp180.jpg", null,
                    cost: 92_000m, life: 15, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Axis Motor Temp", SensorReadingType.Temperature, "°C", 25m, 60m, 70m, 80m, 60, isPrimary: true, order: 10),
                Sensor("Servo Current", SensorReadingType.Current, "A", 3m, 22m, 28m, 32m, 60, isPrimary: true, order: 20),
                Sensor("Gripper Pressure", SensorReadingType.Pressure, "bar", 4m, 7m, 3m, 2m, 60, isPrimary: true, order: 30, breachHigh: false),
                Sensor("Cycle Count", SensorReadingType.Cycles, "cycles", 0m, 99_999_999m, null, null, 60, isPrimary: false, order: 40),
            }
        };

        // ---- 6. Welding Power Source ----
        private static EquipmentClass WeldingPowerSource() => new()
        {
            Code = "WELDING_POWER_SOURCE",
            Name = "Welding Power Source",
            Category = "Welding",
            IconCode = "power-supply",
            Description = "Inverter-based welding power supplies that drive robotic and manual weld torches.",
            DisplayOrder = 60,
            Models =
            {
                Model("Lincoln Electric", "Power Wave S350", "Lincoln Power Wave S350",
                    "https://www.lincolnelectric.com/en/Products/k2823-3",
                    "/assets/equipment/welder/lincoln-pws350.jpg",
                    "/assets/manuals/lincoln-pws350-svc.pdf",
                    cost: 12_500m, life: 12, weight: 5,
                    notes: "Lincoln Electric publishes service and operator PDFs without registration. Storyline asset for PR #117.2 arc-voltage drift scenario."),
                Model("Miller Electric Mfg. LLC", "Dynasty 400", "Miller Dynasty 400",
                    "https://www.millerwelds.com/equipment/welders/tig-gtaw/dynasty-400-series-m00770",
                    "/assets/equipment/welder/miller-dynasty400.jpg",
                    "/assets/manuals/miller-dynasty400.pdf",
                    cost: 9_800m, life: 12, weight: 4),
                Model("ESAB", "Aristo 500ix", "ESAB Aristo 500ix",
                    "https://www.esabna.com/us/en/products/index.cfm?fuseaction=home.product&productCode=aristo-mig-5000ic",
                    "/assets/equipment/welder/esab-aristo500ix.jpg", null,
                    cost: 11_200m, life: 12, weight: 3),
                Model("Fronius USA LLC", "TPS 400i", "Fronius TPS 400i",
                    "https://www.fronius.com/en-us/usa/perfect-welding/products/manual-welding/migmag/tps-i/tps-400i",
                    "/assets/equipment/welder/fronius-tps400i.jpg", null,
                    cost: 13_500m, life: 12, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Arc Voltage", SensorReadingType.Voltage, "V", 18m, 32m, 35m, 38m, 60, isPrimary: true, order: 10,
                    notes: "Arc voltage drift > spec every Nth weld points to contact-tip wear or wire-feed irregularity."),
                Sensor("Welding Current", SensorReadingType.Current, "A", 80m, 320m, 360m, 400m, 60, isPrimary: true, order: 20),
                Sensor("Duty Cycle", SensorReadingType.DutyCycle, "%", 30m, 100m, 110m, 120m, 60, isPrimary: true, order: 30,
                    notes: "Sustained > 100% duty triggers internal thermal cutout — pre-warns of cell over-utilization."),
                Sensor("Power Supply Temp", SensorReadingType.Temperature, "°C", 25m, 55m, 65m, 75m, 60, isPrimary: false, order: 40),
                Sensor("Wire Feed Rate", SensorReadingType.Speed, "in/min", 100m, 500m, null, null, 60, isPrimary: false, order: 50),
            }
        };

        // ---- 7. Stamping Press ----
        private static EquipmentClass StampingPress() => new()
        {
            Code = "STAMPING_PRESS",
            Name = "Hydraulic / Mechanical Stamping Press",
            Category = "Stamping",
            IconCode = "press",
            Description = "Heavy stamping presses for tier-1 automotive body-panel and structural stampings (ANSI B11.1).",
            DisplayOrder = 70,
            Models =
            {
                Model("Schuler", "MSP 400", "Schuler MSP 400",
                    "https://www.schulergroup.com/major/products/servopressen/index.html",
                    "/assets/equipment/press/schuler-msp400.jpg", null,
                    cost: 2_850_000m, life: 30, weight: 2),
                Model("AIDA-America", "NS2-2500", "AIDA NS2-2500",
                    "https://www.aida-global.com/en/products/ns2",
                    "/assets/equipment/press/aida-ns2.jpg", null,
                    cost: 1_950_000m, life: 30, weight: 3),
                Model("Komatsu Industries Corp.", "H1F-200", "Komatsu H1F-200",
                    "https://kic.komatsu/en/products/press_machine/", "/assets/equipment/press/komatsu-h1f200.jpg", null,
                    cost: 1_650_000m, life: 30, weight: 2),
                Model("Minster Machine Co. (Nidec)", "PII-200", "Minster PII-200",
                    "https://www.nidec-arisa.com/", "/assets/equipment/press/minster-pii200.jpg", null,
                    cost: 1_450_000m, life: 30, weight: 2),
            },
            SensorProfiles =
            {
                Sensor("Hydraulic Ram Pressure", SensorReadingType.Pressure, "PSI", 800m, 3000m, 3200m, 3400m, 60, isPrimary: true, order: 10,
                    notes: "Real per-stroke pressure profile from hydraulic press."),
                Sensor("Cycle Count", SensorReadingType.Cycles, "strokes", 0m, 99_999_999m, null, null, 60, isPrimary: true, order: 20,
                    notes: "Cumulative stroke counter — drives die-resharpen and crown overhaul PMs."),
                Sensor("Crown Bearing Temp", SensorReadingType.Temperature, "°C", 30m, 65m, 75m, 85m, 60, isPrimary: true, order: 30),
                Sensor("Hydraulic Oil Temp", SensorReadingType.Temperature, "°C", 35m, 55m, 65m, 75m, 60, isPrimary: false, order: 40),
                Sensor("Vibration RMS", SensorReadingType.Vibration, "mm/s", 1.0m, 4.0m, 5.0m, 7.0m, 60, isPrimary: false, order: 50,
                    notes: "Higher baseline than CNC because of structural impact."),
            }
        };

        // ---- 8. Press Brake ----
        private static EquipmentClass PressBrake() => new()
        {
            Code = "PRESS_BRAKE",
            Name = "CNC Press Brake",
            Category = "Stamping",
            IconCode = "brake",
            Description = "CNC press brakes for sheet metal bending in fabrication cells.",
            DisplayOrder = 80,
            Models =
            {
                Model("Amada America, Inc.", "HG 1303", "Amada HG 1303",
                    "https://www.amada.com/america/hg-1003",
                    "/assets/equipment/brake/amada-hg1303.jpg", null,
                    cost: 285_000m, life: 22, weight: 4),
                Model("TRUMPF Inc.", "TruBend 5130", "TRUMPF TruBend 5130",
                    "https://www.trumpf.com/en_US/products/machines-systems/bending-machines/trubend-series-5000/",
                    "/assets/equipment/brake/trumpf-trubend5130.jpg", null,
                    cost: 320_000m, life: 22, weight: 3),
                Model("Bystronic", "Xpert 150", "Bystronic Xpert 150",
                    "https://www.bystronic.com/en-us/products/bending/press-brakes/xpert/",
                    "/assets/equipment/brake/bystronic-xpert150.jpg", null,
                    cost: 245_000m, life: 22, weight: 2),
            },
            SensorProfiles =
            {
                Sensor("Hydraulic Pressure", SensorReadingType.Pressure, "bar", 80m, 250m, 280m, 300m, 60, isPrimary: true, order: 10),
                Sensor("Cycle Count", SensorReadingType.Cycles, "strokes", 0m, 99_999_999m, null, null, 60, isPrimary: true, order: 20),
                Sensor("Hydraulic Oil Temp", SensorReadingType.Temperature, "°C", 30m, 55m, 65m, 75m, 60, isPrimary: false, order: 30),
                Sensor("Ram Position Drift", SensorReadingType.Load, "%", 0m, 0.5m, 1.0m, 2.0m, 60, isPrimary: false, order: 40,
                    notes: "Position-loop following error; demo-grade proxy for backgauge accuracy drift."),
            }
        };

        // ---- 9. Laser Cutter ----
        private static EquipmentClass LaserCutter() => new()
        {
            Code = "LASER_CUTTER",
            Name = "Fiber Laser Cutter",
            Category = "Stamping",
            IconCode = "laser",
            Description = "Fiber laser cutting systems used for blank prototyping and low-volume sheet metal.",
            DisplayOrder = 90,
            Models =
            {
                Model("TRUMPF Inc.", "TruLaser 5030 fiber", "TRUMPF TruLaser 5030 fiber",
                    "https://www.trumpf.com/en_US/products/machines-systems/2d-laser-cutting-machines/trulaser-series-5000/",
                    "/assets/equipment/laser/trumpf-trulaser5030.jpg", null,
                    cost: 1_250_000m, life: 18, weight: 2),
                Model("Amada America, Inc.", "ENSIS 3015 RI", "Amada ENSIS 3015 RI",
                    "https://www.amada.com/america/ensis-3015-aj-ri",
                    "/assets/equipment/laser/amada-ensis3015.jpg", null,
                    cost: 980_000m, life: 18, weight: 2),
                Model("Bystronic", "ByStar Fiber 6kW", "Bystronic ByStar Fiber 6kW",
                    "https://www.bystronic.com/en-us/products/cutting/fiber-lasers/bystar-fiber/",
                    "/assets/equipment/laser/bystronic-bystar.jpg", null,
                    cost: 1_120_000m, life: 18, weight: 2),
            },
            SensorProfiles =
            {
                Sensor("Resonator Temp", SensorReadingType.Temperature, "°C", 18m, 28m, 32m, 36m, 60, isPrimary: true, order: 10),
                Sensor("Chiller Output Temp", SensorReadingType.Temperature, "°C", 16m, 24m, 28m, 32m, 60, isPrimary: true, order: 20),
                Sensor("Assist Gas Pressure", SensorReadingType.Pressure, "bar", 8m, 25m, 6m, 5m, 60, isPrimary: true, order: 30, breachHigh: false),
                Sensor("Beam Output Power", SensorReadingType.Power, "kW", 1m, 6m, null, null, 60, isPrimary: false, order: 40),
            }
        };

        // ---- 10. Industrial Conveyor ----
        private static EquipmentClass IndustrialConveyor() => new()
        {
            Code = "INDUSTRIAL_CONVEYOR",
            Name = "Industrial Conveyor",
            Category = "Material Handling",
            IconCode = "conveyor",
            Description = "Roller, belt, and chain conveyors moving WIP through stamping → weld → assembly cells.",
            DisplayOrder = 100,
            Models =
            {
                Model("Hytrol Conveyor Company, Inc.", "EZLogic 190E24", "Hytrol EZLogic 190E24",
                    "https://www.hytrol.com/products/190e24/",
                    "/assets/equipment/conveyor/hytrol-190e24.jpg", null,
                    cost: 28_000m, life: 25, weight: 4),
                Model("Dorner", "3200 Series", "Dorner 3200 Series",
                    "https://www.dornerconveyors.com/3200-series-modular-belt-conveyors/",
                    "/assets/equipment/conveyor/dorner-3200.jpg", null,
                    cost: 12_500m, life: 25, weight: 5),
                Model("Interroll", "MultiControl 2.0", "Interroll MultiControl 2.0",
                    "https://www.interroll.com/products/conveyor-modules-rollers/conveyor-control/multicontrol/",
                    "/assets/equipment/conveyor/interroll-multicontrol.jpg", null,
                    cost: 18_500m, life: 25, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Drive Motor Temp", SensorReadingType.Temperature, "°C", 25m, 60m, 70m, 80m, 60, isPrimary: true, order: 10),
                Sensor("Drive Current", SensorReadingType.Current, "A", 1m, 12m, 14m, 16m, 60, isPrimary: true, order: 20),
                Sensor("Belt Speed", SensorReadingType.Speed, "ft/min", 30m, 180m, null, null, 60, isPrimary: false, order: 30),
                Sensor("Drive Vibration", SensorReadingType.Vibration, "mm/s", 0.3m, 2.0m, 3.0m, 4.0m, 60, isPrimary: false, order: 40),
            }
        };

        // ---- 11. Air Compressor ----
        private static EquipmentClass AirCompressor() => new()
        {
            Code = "AIR_COMPRESSOR",
            Name = "Industrial Air Compressor",
            Category = "Facility",
            IconCode = "compressor",
            Description = "Rotary-screw compressors supplying plant compressed air at 100-125 PSI.",
            DisplayOrder = 110,
            Models =
            {
                Model("Atlas Copco USA", "GA 75 VSD+", "Atlas Copco GA 75 VSD+",
                    "https://www.atlascopco.com/en-us/compressors/products/air-compressor/oil-injected-rotary-screw-compressors/ga-vsd",
                    "/assets/equipment/compressor/atlas-ga75vsd.jpg", null,
                    cost: 58_000m, life: 18, weight: 4),
                Model("Ingersoll Rand", "R-Series RS75ie", "Ingersoll Rand RS75ie",
                    "https://www.ingersollrand.com/en-us/air-compressors/products/rotary-screw-air-compressors/r-series",
                    "/assets/equipment/compressor/ir-rs75ie.jpg", null,
                    cost: 49_000m, life: 18, weight: 4),
                Model("Kaeser Compressors, Inc.", "CSD 105", "Kaeser CSD 105",
                    "https://us.kaeser.com/products/rotary-screw-compressors/cs-csd-csdx/",
                    "/assets/equipment/compressor/kaeser-csd105.jpg", null,
                    cost: 52_000m, life: 18, weight: 3),
                Model("Sullair", "ShopTek ST 75", "Sullair ShopTek ST 75",
                    "https://www.sullair.com/en-us/products/rotary-screw-air-compressors/shoptek",
                    "/assets/equipment/compressor/sullair-st75.jpg", null,
                    cost: 41_000m, life: 18, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Discharge Pressure", SensorReadingType.Pressure, "PSI", 100m, 125m, 130m, 140m, 60, isPrimary: true, order: 10),
                Sensor("Discharge Temp", SensorReadingType.Temperature, "°C", 75m, 95m, 105m, 115m, 60, isPrimary: true, order: 20,
                    notes: "Rotary-screw airend; sustained > 105°C indicates oil cooler fouling."),
                Sensor("Motor Current", SensorReadingType.Current, "A", 60m, 120m, 130m, 140m, 60, isPrimary: false, order: 30),
                Sensor("Operating Hours", SensorReadingType.Hours, "hr", 0m, 80_000m, null, null, 60, isPrimary: true, order: 40),
                Sensor("Power Draw", SensorReadingType.Power, "kW", 30m, 75m, null, null, 60, isPrimary: false, order: 50),
            }
        };

        // ---- 12. Forklift ----
        private static EquipmentClass Forklift() => new()
        {
            Code = "FORKLIFT",
            Name = "Forklift (Counterbalance)",
            Category = "Material Handling",
            IconCode = "forklift",
            Description = "Class I (electric) and Class IV/V (LPG/diesel) counterbalance lift trucks (OSHA 1910.178).",
            DisplayOrder = 120,
            Models =
            {
                Model("Hyster Company", "H50FT", "Hyster H50FT",
                    "https://www.hyster.com/en-us/north-america/products/internal-combustion-counterbalance-forklift/h40-70ft/",
                    "/assets/equipment/forklift/hyster-h50ft.jpg",
                    "/assets/manuals/hyster-h50ft-svc.pdf",
                    cost: 39_500m, life: 12, weight: 5),
                Model("Toyota Material Handling", "8FGCU25", "Toyota 8FGCU25",
                    "https://www.toyotaforklift.com/forklifts/internal-combustion-cushion/core-ic-cushion",
                    "/assets/equipment/forklift/toyota-8fgcu25.jpg", null,
                    cost: 36_800m, life: 14, weight: 5),
                Model("Crown Equipment Corporation", "FC 5200 Series", "Crown FC 5200",
                    "https://www.crown.com/en-us/forklifts/fc-5200.html",
                    "/assets/equipment/forklift/crown-fc5200.jpg", null,
                    cost: 42_000m, life: 14, weight: 3),
                Model("Yale Materials Handling", "ERC050VG", "Yale ERC050VG",
                    "https://www.yale.com/en-us/north-america/products/electric-counterbalance-forklift/erc040-070vg/",
                    "/assets/equipment/forklift/yale-erc050vg.jpg", null,
                    cost: 38_500m, life: 12, weight: 4),
            },
            SensorProfiles =
            {
                Sensor("Hour Meter", SensorReadingType.Hours, "hr", 0m, 25_000m, null, null, 60, isPrimary: true, order: 10,
                    notes: "Primary maintenance driver — PM cadence keyed off this."),
                Sensor("Battery State", SensorReadingType.Battery, "%", 20m, 100m, 25m, 15m, 60, isPrimary: true, order: 20, breachHigh: false),
                Sensor("Hydraulic Oil Temp", SensorReadingType.Temperature, "°C", 30m, 65m, 75m, 85m, 60, isPrimary: false, order: 30),
                Sensor("Travel Motor Temp", SensorReadingType.Temperature, "°C", 30m, 70m, 80m, 90m, 60, isPrimary: false, order: 40),
            }
        };

        // ---- 13. HVAC Unit ----
        private static EquipmentClass HvacUnit() => new()
        {
            Code = "HVAC_UNIT",
            Name = "HVAC Rooftop Unit",
            Category = "Facility",
            IconCode = "hvac",
            Description = "Packaged rooftop HVAC units conditioning office and clean assembly areas (EPA 40 CFR Part 82 refrigerant tracking).",
            DisplayOrder = 130,
            Models =
            {
                Model("Trane Technologies", "Precedent 5-Ton Heat Pump", "Trane Precedent (5 ton)",
                    "https://www.trane.com/commercial/north-america/us/en/products-systems/equipment/light-commercial/precedent.html",
                    "/assets/equipment/hvac/trane-precedent.jpg", null,
                    cost: 14_500m, life: 18, weight: 5),
                Model("Carrier Corporation", "WeatherMaker 48TC", "Carrier WeatherMaker 48TC",
                    "https://www.carrier.com/commercial/en/us/products/light-commercial/rooftop-units/weathermaker-48tc/",
                    "/assets/equipment/hvac/carrier-48tc.jpg", null,
                    cost: 13_800m, life: 18, weight: 4),
                Model("Lennox International", "Energence 10-Ton", "Lennox Energence (10 ton)",
                    "https://www.lennoxcommercial.com/products/packaged-rooftop-units/energence-rooftop-units",
                    "/assets/equipment/hvac/lennox-energence.jpg", null,
                    cost: 22_000m, life: 18, weight: 3),
            },
            SensorProfiles =
            {
                Sensor("Supply Air Temp", SensorReadingType.Temperature, "°F", 50m, 75m, 80m, 85m, 60, isPrimary: true, order: 10),
                Sensor("Return Air Temp", SensorReadingType.Temperature, "°F", 65m, 80m, null, null, 60, isPrimary: false, order: 20),
                Sensor("Suction Pressure", SensorReadingType.Pressure, "PSI", 60m, 85m, 50m, 40m, 60, isPrimary: true, order: 30, breachHigh: false,
                    notes: "Suction pressure drop is the classic refrigerant-loss signature."),
                Sensor("Discharge Pressure", SensorReadingType.Pressure, "PSI", 200m, 350m, 380m, 420m, 60, isPrimary: false, order: 40),
                Sensor("Compressor Current", SensorReadingType.Current, "A", 10m, 35m, 40m, 45m, 60, isPrimary: false, order: 50),
                Sensor("Filter Pressure Drop", SensorReadingType.Pressure, "inWC", 0.2m, 0.8m, 1.0m, 1.5m, 60, isPrimary: false, order: 60),
            }
        };

        // ---- 14. Coordinate Measuring Machine ----
        private static EquipmentClass Cmm() => new()
        {
            Code = "CMM",
            Name = "Coordinate Measuring Machine",
            Category = "Measurement",
            IconCode = "cmm",
            Description = "Bridge and gantry CMMs for first-article and CTQ inspection (ISO 10360 calibration cadence).",
            DisplayOrder = 140,
            Models =
            {
                Model("Zeiss Industrial Metrology", "CONTURA G2", "Zeiss CONTURA G2",
                    "https://www.zeiss.com/metrology/products/systems/bridge-type-cmms/contura.html",
                    "/assets/equipment/cmm/zeiss-contura.jpg", null,
                    cost: 195_000m, life: 25, weight: 3),
                Model("Hexagon Manufacturing Intelligence", "GLOBAL S 09.15.08", "Hexagon GLOBAL S",
                    "https://hexagon.com/products/global-s",
                    "/assets/equipment/cmm/hexagon-global-s.jpg", null,
                    cost: 175_000m, life: 25, weight: 3),
                Model("Mitutoyo America Corporation", "CRYSTA-Apex V", "Mitutoyo CRYSTA-Apex V",
                    "https://www.mitutoyo.com/products/coordinate-measuring-machines/crysta-apex-v/",
                    "/assets/equipment/cmm/mitutoyo-crysta-apex-v.jpg", null,
                    cost: 145_000m, life: 25, weight: 4),
            },
            SensorProfiles =
            {
                Sensor("Granite Bed Temp", SensorReadingType.Temperature, "°C", 19m, 22m, 23m, 24m, 60, isPrimary: true, order: 10,
                    notes: "20 °C ± 1 °C is the metrology gold standard. Drift beyond ± 2 °C invalidates measurements."),
                Sensor("Ambient Humidity", SensorReadingType.Humidity, "% RH", 35m, 60m, 65m, 70m, 60, isPrimary: true, order: 20),
                Sensor("Air Bearing Pressure", SensorReadingType.Pressure, "bar", 4.5m, 5.5m, 4.2m, 4.0m, 60, isPrimary: true, order: 30, breachHigh: false),
                Sensor("Probe Cycles Since Cal", SensorReadingType.Cycles, "cycles", 0m, 50_000m, null, null, 60, isPrimary: false, order: 40),
            }
        };

        // ----- Helper factories -----

        private static EquipmentModel Model(
            string manufacturer, string modelNumber, string displayName,
            string? productUrl, string? imageUrl, string? manualUrl,
            decimal? cost = null, int? life = null, int weight = 1, string? notes = null)
            => new()
            {
                Manufacturer = manufacturer,
                ModelNumber = modelNumber,
                DisplayName = displayName,
                ProductPageUrl = productUrl,
                ImageUrl = imageUrl,
                MaintenanceManualUrl = manualUrl,
                TypicalAcquisitionCost = cost,
                ServiceLifeYears = life,
                Weight = weight,
                Notes = notes,
            };

        private static SensorProfile Sensor(
            string name,
            SensorReadingType type,
            string unit,
            decimal normalMin,
            decimal normalMax,
            decimal? warning,
            decimal? critical,
            int sampleRateMinutes,
            bool isPrimary,
            int order,
            bool breachHigh = true,
            string? notes = null)
            => new()
            {
                SensorName = name,
                ReadingType = type,
                Unit = unit,
                NormalMin = normalMin,
                NormalMax = normalMax,
                WarningThreshold = warning,
                CriticalThreshold = critical,
                SampleRateMinutes = sampleRateMinutes,
                IsPrimary = isPrimary,
                DisplayOrder = order,
                BreachOnHighSide = breachHigh,
                Notes = notes,
            };
    }
}
