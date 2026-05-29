using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class B11_R1_1_DepartmentOrgBackbone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultCalendarId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProductionDepartment",
                table: "Departments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentDepartmentId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannerId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupervisorId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Departments",
                type: "xid",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCenters_OwningDepartment_Partial",
                table: "WorkCenters",
                column: "OwningDepartmentId",
                filter: "\"OwningDepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsProduction_Partial",
                table: "Departments",
                column: "IsProductionDepartment",
                filter: "\"IsProductionDepartment\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Parent_Partial",
                table: "Departments",
                column: "ParentDepartmentId",
                filter: "\"ParentDepartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Site_Partial",
                table: "Departments",
                column: "SiteId",
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkCenters_Departments_OwningDepartmentId",
                table: "WorkCenters",
                column: "OwningDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Departments_ParentDepartmentId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkCenters_Departments_OwningDepartmentId",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_WorkCenters_OwningDepartment_Partial",
                table: "WorkCenters");

            migrationBuilder.DropIndex(
                name: "IX_Departments_IsProduction_Partial",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Parent_Partial",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Site_Partial",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "DefaultCalendarId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsProductionDepartment",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ParentDepartmentId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "PlannerId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "SupervisorId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Departments");
        }
    }
}
