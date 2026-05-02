using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddMesIotOeeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amperage",
                table: "Assets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualEnergyConsumptionKWH",
                table: "Assets",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetType",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationCertificateNumber",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalibrationFrequencyDays",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CalibrationRequired",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationStatus",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationType",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationVendor",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Capacity",
                table: "Assets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapacityUOM",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CellId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "Assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ConfinedSpaceEntry",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentAvailability",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentMeterReading",
                table: "Assets",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentOEE",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPerformance",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPressure",
                table: "Assets",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentQuality",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentTemperature",
                table: "Assets",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentVibration",
                table: "Assets",
                type: "numeric(8,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataHistorianTag",
                table: "Assets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dimensions",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisposalMethod",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisposalReason",
                table: "Assets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EPAPermitNumber",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmissionsMonitored",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EnergyClass",
                table: "Assets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnergyMeterId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentalClass",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureClassId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GLAccumDepAccount",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GLAssetAccount",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GLDepExpenseAccount",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMeter",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasStandbyMode",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HealthScoreLastCalculated",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HighVoltage",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Horsepower",
                table: "Assets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HotWorkPermitRequired",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "Assets",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IdealCycleTimeSeconds",
                table: "Assets",
                type: "numeric(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IdlePowerConsumptionKW",
                table: "Assets",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InstallDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuredValue",
                table: "Assets",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IoTConnectionStatus",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IoTDeviceId",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IoTEnabled",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IoTEndpointUrl",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IoTGatewayId",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IoTPollingIntervalSeconds",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IoTProtocol",
                table: "Assets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLinear",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRotating",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "KilowattRating",
                table: "Assets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LOTOProcedureId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCalibrationDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastIoTCommunication",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMeterReadingDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LockoutTagoutRequired",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LongDescription",
                table: "Assets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MACAddress",
                table: "Assets",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeterType",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextCalibrationDue",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Assets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OEELastCalculated",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OEETracked",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OSHAClassification",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentAssetId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedProductionHoursPerDay",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PredictedFailureDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PredictedFailureReason",
                table: "Assets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PredictiveHealthScore",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PressureAlarmThreshold",
                table: "Assets",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PressureUOM",
                table: "Assets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PressureWarningThreshold",
                table: "Assets",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProcessId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionLineId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionLineName",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNumber",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RPM",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatedPowerConsumptionKW",
                table: "Assets",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementCost",
                table: "Assets",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RoutingSequence",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Row",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SCADATag",
                table: "Assets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyClassification",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyNotes",
                table: "Assets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SensorReadingsLastUpdated",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShiftCalendarId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandardRunRate",
                table: "Assets",
                type: "numeric(12,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardRunRateUOM",
                table: "Assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StandbyPowerConsumptionKW",
                table: "Assets",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagNumber",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetAvailability",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetOEE",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetPerformance",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetQuality",
                table: "Assets",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureAlarmThreshold",
                table: "Assets",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureWarningThreshold",
                table: "Assets",
                type: "numeric(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VibrationAlarmThreshold",
                table: "Assets",
                type: "numeric(8,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VibrationWarningThreshold",
                table: "Assets",
                type: "numeric(8,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Voltage",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyContractNumber",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyEndDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyStartDate",
                table: "Assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyVendorId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Assets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightUOM",
                table: "Assets",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCenterId",
                table: "Assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCenterName",
                table: "Assets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActionCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    RequiresParts = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "integer", nullable: false),
                    ApproverRoles = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApproverUserIds = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequireSequentialApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AutoApproveIfBelowThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CauseCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauseCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CauseCodes_CauseCodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CauseCodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Crafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DefaultHourlyRate = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiresCertification = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredCertifications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: false),
                    IsBaseCurrency = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailureCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailureCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FailureCodes_FailureCodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FailureCodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LaborTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    MultiplierRate = table.Column<decimal>(type: "numeric", nullable: false),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTypeCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsPreventive = table.Column<bool>(type: "boolean", nullable: false),
                    IsCorrective = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTypeCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberingSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Suffix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NextNumber = table.Column<int>(type: "integer", nullable: false),
                    NumberLength = table.Column<int>(type: "integer", nullable: false),
                    PadWithZeros = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeYear = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeMonth = table.Column<bool>(type: "boolean", nullable: false),
                    ResetYearly = table.Column<bool>(type: "boolean", nullable: false),
                    ResetMonthly = table.Column<bool>(type: "boolean", nullable: false),
                    LastResetYear = table.Column<int>(type: "integer", nullable: true),
                    LastResetMonth = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DueDays = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    DiscountDays = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriorityLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "integer", nullable: false),
                    TargetCompletionHours = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProblemCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    DefaultSeverity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Carrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EstimatedDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultCost = table.Column<decimal>(type: "numeric", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TaxAuthority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true),
                    IsRecoverable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UOMDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsBaseUnit = table.Column<bool>(type: "boolean", nullable: false),
                    BaseUnitId = table.Column<int>(type: "integer", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UOMDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalThreshold = table.Column<decimal>(type: "numeric", nullable: true),
                    DefaultPriorityId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CraftId = table.Column<int>(type: "integer", nullable: true),
                    RequiresTraining = table.Column<bool>(type: "boolean", nullable: false),
                    TrainingHoursRequired = table.Column<int>(type: "integer", nullable: true),
                    RequiresCertification = table.Column<bool>(type: "boolean", nullable: false),
                    CertificationName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CertificationValidityMonths = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Crafts_CraftId",
                        column: x => x.CraftId,
                        principalTable: "Crafts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LaborRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CraftId = table.Column<int>(type: "integer", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    StandardRate = table.Column<decimal>(type: "numeric", nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    DoubleTimeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborRates_Crafts_CraftId",
                        column: x => x.CraftId,
                        principalTable: "Crafts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LaborRates_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ParentAssetId",
                table: "Assets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CauseCodes_ParentId",
                table: "CauseCodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FailureCodes_ParentId",
                table: "FailureCodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborRates_CraftId",
                table: "LaborRates",
                column: "CraftId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborRates_SkillId",
                table: "LaborRates",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CraftId",
                table: "Skills",
                column: "CraftId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Assets_ParentAssetId",
                table: "Assets",
                column: "ParentAssetId",
                principalTable: "Assets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Assets_ParentAssetId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "ActionCodes");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows");

            migrationBuilder.DropTable(
                name: "CauseCodes");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "FailureCodes");

            migrationBuilder.DropTable(
                name: "LaborRates");

            migrationBuilder.DropTable(
                name: "LaborTypes");

            migrationBuilder.DropTable(
                name: "MaintenanceTypeCodes");

            migrationBuilder.DropTable(
                name: "NumberingSequences");

            migrationBuilder.DropTable(
                name: "PaymentTerms");

            migrationBuilder.DropTable(
                name: "PriorityLevels");

            migrationBuilder.DropTable(
                name: "ProblemCodes");

            migrationBuilder.DropTable(
                name: "ShippingMethods");

            migrationBuilder.DropTable(
                name: "TaxCodes");

            migrationBuilder.DropTable(
                name: "UOMDefinitions");

            migrationBuilder.DropTable(
                name: "WorkOrderTypes");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Crafts");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ParentAssetId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Aisle",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Amperage",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AnnualEnergyConsumptionKWH",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AssetType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationCertificateNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationFrequencyDays",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationRequired",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CalibrationVendor",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CapacityUOM",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CellId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ConfinedSpaceEntry",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentAvailability",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentMeterReading",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentOEE",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentPerformance",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentPressure",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentQuality",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentTemperature",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CurrentVibration",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DataHistorianTag",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Dimensions",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalMethod",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalReason",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "EPAPermitNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "EmissionsMonitored",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "EnergyClass",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "EnergyMeterId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "EnvironmentalClass",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FailureClassId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "GLAccumDepAccount",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "GLAssetAccount",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "GLDepExpenseAccount",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "HasMeter",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "HasStandbyMode",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "HealthScoreLastCalculated",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "HighVoltage",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Horsepower",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "HotWorkPermitRequired",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IdealCycleTimeSeconds",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IdlePowerConsumptionKW",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InstallDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InsuredValue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTConnectionStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTDeviceId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTEnabled",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTEndpointUrl",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTGatewayId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTPollingIntervalSeconds",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IoTProtocol",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsLinear",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsRotating",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "KilowattRating",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LOTOProcedureId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LastCalibrationDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LastIoTCommunication",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LastMeterReadingDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LockoutTagoutRequired",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LongDescription",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "MACAddress",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "MeterType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "NextCalibrationDue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OEELastCalculated",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OEETracked",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OSHAClassification",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ParentAssetId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PlannedProductionHoursPerDay",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PredictedFailureDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PredictedFailureReason",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PredictiveHealthScore",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PressureAlarmThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PressureUOM",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PressureWarningThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ProductionLineId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ProductionLineName",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RPM",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RatedPowerConsumptionKW",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ReplacementCost",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RoutingSequence",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Row",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SCADATag",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SafetyClassification",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SafetyNotes",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SensorReadingsLastUpdated",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ShiftCalendarId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StandardRunRate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StandardRunRateUOM",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StandbyPowerConsumptionKW",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TagNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TargetAvailability",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TargetOEE",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TargetPerformance",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TargetQuality",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TemperatureAlarmThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TemperatureWarningThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VibrationAlarmThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VibrationWarningThreshold",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Voltage",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyContractNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyEndDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyStartDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyVendorId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WeightUOM",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WorkCenterId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WorkCenterName",
                table: "Assets");
        }
    }
}
