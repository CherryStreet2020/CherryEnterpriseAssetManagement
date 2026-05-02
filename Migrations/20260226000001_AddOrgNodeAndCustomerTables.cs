using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Abs.FixedAssets.Migrations
{
    public partial class AddOrgNodeAndCustomerTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS platform;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS platform.org_node (
                    id uuid NOT NULL DEFAULT gen_random_uuid(),
                    tenant_code text NOT NULL DEFAULT 'default',
                    node_type text NOT NULL DEFAULT 'location',
                    name text NOT NULL,
                    code text NULL,
                    parent_id uuid NULL,
                    company_id integer NULL,
                    site_id integer NULL,
                    location_id integer NULL,
                    is_active boolean NOT NULL DEFAULT true,
                    sort_order integer NOT NULL DEFAULT 0,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_org_node"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_org_node_parent"" FOREIGN KEY (parent_id) REFERENCES platform.org_node(id) ON DELETE RESTRICT
                );

                CREATE INDEX IF NOT EXISTS ""IX_org_node_tenant_code_node_type"" ON platform.org_node (tenant_code, node_type);
                CREATE INDEX IF NOT EXISTS ""IX_org_node_parent_id"" ON platform.org_node (parent_id);
                CREATE INDEX IF NOT EXISTS ""IX_org_node_company_id"" ON platform.org_node (company_id);
                CREATE INDEX IF NOT EXISTS ""IX_org_node_site_id"" ON platform.org_node (site_id);
                CREATE INDEX IF NOT EXISTS ""IX_org_node_location_id"" ON platform.org_node (location_id);
            ");

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(nullable: false),
                    CustomerCode = table.Column<string>(maxLength: 20, nullable: false),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(maxLength: 20, nullable: true),
                    Address = table.Column<string>(maxLength: 200, nullable: true),
                    City = table.Column<string>(maxLength: 100, nullable: true),
                    StateProvince = table.Column<string>(maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(maxLength: 20, nullable: true),
                    Country = table.Column<string>(maxLength: 50, nullable: true),
                    TaxId = table.Column<string>(maxLength: 50, nullable: true),
                    Currency = table.Column<string>(maxLength: 3, nullable: false, defaultValue: "USD"),
                    PaymentTermId = table.Column<int>(nullable: true),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey("FK_Customers_Companies_CompanyId", x => x.CompanyId, "Companies", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_Customers_CompanyId_CustomerCode", "Customers", new[] { "CompanyId", "CustomerCode" }, unique: true);

            migrationBuilder.CreateTable(
                name: "CustomerInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(nullable: false),
                    CustomerId = table.Column<int>(nullable: false),
                    InvoiceNumber = table.Column<string>(maxLength: 30, nullable: false),
                    InvoiceDate = table.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
                    DueDate = table.Column<DateTime>(nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    BalanceDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Draft"),
                    StatusLookupValueId = table.Column<int>(nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: true),
                    PurchaseOrderRef = table.Column<string>(maxLength: 50, nullable: true),
                    SiteId = table.Column<int>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvoices", x => x.Id);
                    table.ForeignKey("FK_CustomerInvoices_Companies_CompanyId", x => x.CompanyId, "Companies", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_CustomerInvoices_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_CustomerInvoices_CompanyId_InvoiceNumber", "CustomerInvoices", new[] { "CompanyId", "InvoiceNumber" }, unique: true);

            migrationBuilder.CreateTable(
                name: "CustomerInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerInvoiceId = table.Column<int>(nullable: false),
                    LineNumber = table.Column<int>(nullable: false),
                    Description = table.Column<string>(maxLength: 200, nullable: true),
                    ItemId = table.Column<int>(nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UOM = table.Column<string>(maxLength: 20, nullable: true),
                    GlAccountId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvoiceLines", x => x.Id);
                    table.ForeignKey("FK_CustomerInvoiceLines_CustomerInvoices_CustomerInvoiceId", x => x.CustomerInvoiceId, "CustomerInvoices", "Id", onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("CustomerInvoiceLines");
            migrationBuilder.DropTable("CustomerInvoices");
            migrationBuilder.DropTable("Customers");
            migrationBuilder.Sql("DROP TABLE IF EXISTS platform.org_node;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS platform;");
        }
    }
}
