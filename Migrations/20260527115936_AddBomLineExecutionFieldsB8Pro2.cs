using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddBomLineExecutionFieldsB8Pro2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlternateBomLineId",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BackflushOperationSequence",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConsumedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ConsumingOperationSequence",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostBucket",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerChargeable",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HeatNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsigned",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerSupplied",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHazardous",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHeatCertRequired",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLongLead",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLotControlled",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSerialControlled",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShelfLifeControlled",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IssueTiming",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "IssuedLotNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IssuedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IssuedSerialNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KitGroup",
                table: "ProductionMaterialStructures",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineStatus",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OverIssuedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PickedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReservedLotNumber",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ScrappedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StagedQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SubstituteAllowed",
                table: "ProductionMaterialStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SubstituteReason",
                table: "ProductionMaterialStructures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubstitutionAuthReference",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplyType",
                table: "ProductionMaterialStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferableQuantity",
                table: "ProductionMaterialStructures",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VendorLot",
                table: "ProductionMaterialStructures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_ConsumingOpSeq",
                table: "ProductionMaterialStructures",
                column: "ConsumingOperationSequence");

            migrationBuilder.CreateIndex(
                name: "IX_ProdMatStruct_LineStatus",
                table: "ProductionMaterialStructures",
                column: "LineStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialStructures_AlternateBomLineId",
                table: "ProductionMaterialStructures",
                column: "AlternateBomLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionMaterialStructures_ProductionMaterialStructures_A~",
                table: "ProductionMaterialStructures",
                column: "AlternateBomLineId",
                principalTable: "ProductionMaterialStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionMaterialStructures_ProductionMaterialStructures_A~",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_ConsumingOpSeq",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProdMatStruct_LineStatus",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropIndex(
                name: "IX_ProductionMaterialStructures_AlternateBomLineId",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "AlternateBomLineId",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "BackflushOperationSequence",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "CertificateNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ConsumedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ConsumingOperationSequence",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "CostBucket",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "CustomerChargeable",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "HeatNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsConsigned",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsCustomerSupplied",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsHazardous",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsHeatCertRequired",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsLongLead",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsLotControlled",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsSerialControlled",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IsShelfLifeControlled",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IssueTiming",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IssuedLotNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IssuedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "IssuedSerialNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "KitGroup",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "LineStatus",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "OverIssuedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "PickedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ReservedLotNumber",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ScrappedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "ShortQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "StagedQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SubstituteAllowed",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SubstituteReason",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SubstitutionAuthReference",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "SupplyType",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "TransferableQuantity",
                table: "ProductionMaterialStructures");

            migrationBuilder.DropColumn(
                name: "VendorLot",
                table: "ProductionMaterialStructures");
        }
    }
}
