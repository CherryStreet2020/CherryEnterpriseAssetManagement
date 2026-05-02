using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    public partial class AddLookupValueForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookTypeLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "POTypeLookupValueId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostTypeLookupValueId",
                table: "CipCosts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "MaintenanceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityLookupValueId",
                table: "MaintenanceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_BookTypeLookupValueId",
                table: "Books",
                column: "BookTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_POTypeLookupValueId",
                table: "PurchaseOrders",
                column: "POTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CipCosts_CostTypeLookupValueId",
                table: "CipCosts",
                column: "CostTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_TypeLookupValueId",
                table: "MaintenanceEvents",
                column: "TypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_PriorityLookupValueId",
                table: "MaintenanceEvents",
                column: "PriorityLookupValueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_BookTypeLookupValueId",
                table: "Books",
                column: "BookTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_LookupValues_POTypeLookupValueId",
                table: "PurchaseOrders",
                column: "POTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CipCosts_LookupValues_CostTypeLookupValueId",
                table: "CipCosts",
                column: "CostTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_TypeLookupValueId",
                table: "MaintenanceEvents",
                column: "TypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_PriorityLookupValueId",
                table: "MaintenanceEvents",
                column: "PriorityLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
                UPDATE ""Books"" b
                SET ""BookTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'BookType'
                  AND lv.""Code"" = CAST(b.""BookType"" AS TEXT);

                UPDATE ""PurchaseOrders"" po
                SET ""POTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'PurchaseOrderType'
                  AND lv.""Code"" = CAST(po.""POType"" AS TEXT);

                UPDATE ""CipCosts"" cc
                SET ""CostTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'CipCostType'
                  AND lv.""Code"" = CAST(cc.""CostType"" AS TEXT);

                UPDATE ""MaintenanceEvents"" me
                SET ""TypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'MaintenanceType'
                  AND lv.""Code"" = CAST(me.""Type"" AS TEXT);

                UPDATE ""MaintenanceEvents"" me
                SET ""PriorityLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'MaintenancePriority'
                  AND lv.""Code"" = CAST(me.""Priority"" AS TEXT);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Books_LookupValues_BookTypeLookupValueId", table: "Books");
            migrationBuilder.DropForeignKey(name: "FK_PurchaseOrders_LookupValues_POTypeLookupValueId", table: "PurchaseOrders");
            migrationBuilder.DropForeignKey(name: "FK_CipCosts_LookupValues_CostTypeLookupValueId", table: "CipCosts");
            migrationBuilder.DropForeignKey(name: "FK_MaintenanceEvents_LookupValues_TypeLookupValueId", table: "MaintenanceEvents");
            migrationBuilder.DropForeignKey(name: "FK_MaintenanceEvents_LookupValues_PriorityLookupValueId", table: "MaintenanceEvents");

            migrationBuilder.DropIndex(name: "IX_Books_BookTypeLookupValueId", table: "Books");
            migrationBuilder.DropIndex(name: "IX_PurchaseOrders_POTypeLookupValueId", table: "PurchaseOrders");
            migrationBuilder.DropIndex(name: "IX_CipCosts_CostTypeLookupValueId", table: "CipCosts");
            migrationBuilder.DropIndex(name: "IX_MaintenanceEvents_TypeLookupValueId", table: "MaintenanceEvents");
            migrationBuilder.DropIndex(name: "IX_MaintenanceEvents_PriorityLookupValueId", table: "MaintenanceEvents");

            migrationBuilder.DropColumn(name: "BookTypeLookupValueId", table: "Books");
            migrationBuilder.DropColumn(name: "POTypeLookupValueId", table: "PurchaseOrders");
            migrationBuilder.DropColumn(name: "CostTypeLookupValueId", table: "CipCosts");
            migrationBuilder.DropColumn(name: "TypeLookupValueId", table: "MaintenanceEvents");
            migrationBuilder.DropColumn(name: "PriorityLookupValueId", table: "MaintenanceEvents");
        }
    }
}
