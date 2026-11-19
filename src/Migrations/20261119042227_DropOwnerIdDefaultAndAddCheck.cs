using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Migrations
{
    /// <inheritdoc />
    public partial class DropOwnerIdDefaultAndAddCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Orders\" ALTER COLUMN \"OwnerId\" DROP DEFAULT;");
            
            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_OwnerId_NotEmpty",
                table: "Orders",
                sql: "\"OwnerId\" <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_OwnerId_NotEmpty",
                table: "Orders");
            
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid");
            
        }
    }
}
