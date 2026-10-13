using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "skills");

            migrationBuilder.CreateTable(
                name: "skill_categories",
                schema: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_skills_skill_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "skills",
                        principalTable: "skill_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_skills",
                schema: "skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_skills_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "skills",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "skills",
                table: "skill_categories",
                columns: new[] { "id", "name", "slug", "sort_order" },
                values: new object[,]
                {
                    { new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), "Spor ve hareket", "spor-ve-hareket", 5 },
                    { new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), "Yazılım ve teknoloji", "yazilim-ve-teknoloji", 3 },
                    { new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), "El işleri ve zanaat", "el-isleri-ve-zanaat", 7 },
                    { new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), "Mutfak", "mutfak", 6 },
                    { new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), "İş ve kariyer", "is-ve-kariyer", 9 },
                    { new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), "Diller", "diller", 1 },
                    { new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), "Tasarım ve görsel sanatlar", "tasarim-ve-gorsel-sanatlar", 4 },
                    { new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), "Akademik", "akademik", 8 },
                    { new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), "Yaşam ve hobi", "yasam-ve-hobi", 10 },
                    { new Guid("f2000328-5332-4247-7109-761847948afd"), "Müzik", "muzik", 2 }
                });

            migrationBuilder.InsertData(
                schema: "skills",
                table: "skills",
                columns: new[] { "id", "category_id", "created_at", "name", "proposed_by_user_id", "reviewed_at", "slug", "status" },
                values: new object[,]
                {
                    { new Guid("037e04ae-2ef4-c176-c1a8-11a56fe1a0f6"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Korece", null, null, "korece", "Approved" },
                    { new Guid("03db0b5b-8a45-9e2b-9ba0-0c0727af3cf5"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Fizik", null, null, "fizik", "Approved" },
                    { new Guid("07a63067-970e-63d6-25cc-d183f81c7986"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Piyano", null, null, "piyano", "Approved" },
                    { new Guid("0ab5c7ae-e267-f08e-6274-af4f9cea8094"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Girişimcilik", null, null, "girisimcilik", "Approved" },
                    { new Guid("0e38be52-2ee6-a6fd-33a9-f54e9082209e"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Müzik prodüksiyonu", null, null, "muzik-produksiyonu", "Approved" },
                    { new Guid("0f71770e-102e-2a6a-e297-303e76527479"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Gitar", null, null, "gitar", "Approved" },
                    { new Guid("1011570c-29be-3594-3422-8ebf3f2a014b"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Seramik", null, null, "seramik", "Approved" },
                    { new Guid("1a983671-91f9-edcc-ca01-523a74056f8e"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tarih", null, null, "tarih", "Approved" },
                    { new Guid("1acb9968-9e85-83f9-eeba-38acc1807390"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Fitness", null, null, "fitness", "Approved" },
                    { new Guid("1bd54525-7dab-8917-773a-1977d9f85448"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Yoga", null, null, "yoga", "Approved" },
                    { new Guid("204844ca-4adf-c021-9612-ead619a5f20e"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ekmek yapımı", null, null, "ekmek-yapimi", "Approved" },
                    { new Guid("222f03f0-d16f-1665-1891-46a4d67813e9"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bağlama", null, null, "baglama", "Approved" },
                    { new Guid("248246de-0973-3500-4254-9366b3717fdc"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Matematik", null, null, "matematik", "Approved" },
                    { new Guid("355a0fdd-0b60-5afb-971b-60b7c9ab3a6b"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "İşaret dili", null, null, "isaret-dili", "Approved" },
                    { new Guid("384d49d4-1b04-9674-28f5-ce24b2517c09"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ebru", null, null, "ebru", "Approved" },
                    { new Guid("3863aadf-befe-7786-12a5-b6a0a2eaffd0"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Yapay zekâ ve makine öğrenmesi", null, null, "yapay-zeka-ve-makine-ogrenmesi", "Approved" },
                    { new Guid("391fccc9-fee8-9b01-61c0-7b0d35dc2c30"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Almanca", null, null, "almanca", "Approved" },
                    { new Guid("3c8ffc9f-eb94-2059-40a0-edd3a08e9bb4"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Örgü", null, null, "orgu", "Approved" },
                    { new Guid("3e70f31e-9d5e-b44e-cb59-1702175a1bbb"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UI/UX tasarımı", null, null, "ui-ux-tasarimi", "Approved" },
                    { new Guid("443d5d03-e7f1-cd0d-2dbe-b68e55b29f32"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "İngilizce", null, null, "ingilizce", "Approved" },
                    { new Guid("448cb129-76b7-4c1a-f156-e57c5d8a2c9b"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Takı tasarımı", null, null, "taki-tasarimi", "Approved" },
                    { new Guid("4afedd44-cdc8-27b9-f3ec-b50b3f9bfbc1"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Python", null, null, "python", "Approved" },
                    { new Guid("4bea7aef-9dc6-13bf-8609-e6568a5f95b0"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "İspanyolca", null, null, "ispanyolca", "Approved" },
                    { new Guid("56443a2a-5245-d63f-4fb9-a2a2c7966b30"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dünya mutfakları", null, null, "dunya-mutfaklari", "Approved" },
                    { new Guid("577ef3cf-c0f7-b818-89f1-80dc000d0e52"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Resim", null, null, "resim", "Approved" },
                    { new Guid("59c16526-7b8a-531d-e603-9642b8279e45"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Fotoğrafçılık", null, null, "fotografcilik", "Approved" },
                    { new Guid("5ad0f658-de0d-71df-7b48-2a2b6b434a6c"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Meditasyon", null, null, "meditasyon", "Approved" },
                    { new Guid("5b917e34-010f-6072-619b-6c49c6165cf4"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Topluluk önünde konuşma", null, null, "topluluk-onunde-konusma", "Approved" },
                    { new Guid("5bd20a76-3f1b-1d7c-84b1-c2a83efbeea2"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "JavaScript", null, null, "javascript", "Approved" },
                    { new Guid("5dd132a2-5ef3-90a1-a798-e30d98fc2126"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bahçecilik", null, null, "bahcecilik", "Approved" },
                    { new Guid("6037e1f8-a583-c99c-7cd3-f5a41ec5b839"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "3D modelleme", null, null, "3d-modelleme", "Approved" },
                    { new Guid("61a07c0d-37a4-cb76-ef17-5614c52f5075"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ney", null, null, "ney", "Approved" },
                    { new Guid("6222759e-906b-d3a4-6bce-3292eae07b91"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ahşap işleri", null, null, "ahsap-isleri", "Approved" },
                    { new Guid("665d8bef-caed-bf39-fc2a-f99ed5ebe528"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Mobil uygulama geliştirme", null, null, "mobil-uygulama-gelistirme", "Approved" },
                    { new Guid("699798a5-bb3e-fb7c-a991-2d0238ce71d1"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pilates", null, null, "pilates", "Approved" },
                    { new Guid("6ee4da22-2c09-d2bc-5887-ce49c3ad2f21"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Şan", null, null, "san", "Approved" },
                    { new Guid("708e9791-4445-9f79-1050-47e05d072070"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bateri", null, null, "bateri", "Approved" },
                    { new Guid("734af269-3ec6-d585-68b3-4acf375791fb"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Edebiyat", null, null, "edebiyat", "Approved" },
                    { new Guid("7384dc1e-35d0-ef08-21b2-6cb6b389f3c9"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Excel", null, null, "excel", "Approved" },
                    { new Guid("73fd57cd-9275-9973-aa52-17ece60c39c2"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pastacılık", null, null, "pastacilik", "Approved" },
                    { new Guid("75f77bef-a61b-4ffb-911d-c4636a17f8cc"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Satranç", null, null, "satranc", "Approved" },
                    { new Guid("78e25c2a-0b1b-8fe1-02ef-5a429d90b53a"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Japonca", null, null, "japonca", "Approved" },
                    { new Guid("7e7d4d8e-6902-47e4-624f-c56552e7f48c"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Arapça", null, null, "arapca", "Approved" },
                    { new Guid("8ee2f498-e259-161b-e96c-388cf0ec2627"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kahve demleme", null, null, "kahve-demleme", "Approved" },
                    { new Guid("951af159-3dd6-f854-1713-48e7a62395eb"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Türk mutfağı", null, null, "turk-mutfagi", "Approved" },
                    { new Guid("966dcaf4-1884-c4ea-b53e-c8d0bab78257"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Koşu", null, null, "kosu", "Approved" },
                    { new Guid("9937c425-9960-b7f4-e659-4c16030a2d1d"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dans", null, null, "dans", "Approved" },
                    { new Guid("a217261f-4e54-9e57-90e6-6d3ddfa25cb2"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Video düzenleme", null, null, "video-duzenleme", "Approved" },
                    { new Guid("a7ab7085-0c2a-3abb-f94e-b8287cdeaa82"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Grafik tasarım", null, null, "grafik-tasarim", "Approved" },
                    { new Guid("aa4ac3f7-1917-b82f-e253-3ef20065d597"), new Guid("486095a4-b6fd-e457-6e20-046401d4388f"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Vegan mutfak", null, null, "vegan-mutfak", "Approved" },
                    { new Guid("af3fac43-9813-aace-d216-3ba0344d1dd7"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Veri analizi", null, null, "veri-analizi", "Approved" },
                    { new Guid("b15031b7-4ec2-739b-9a65-387cc32866f2"), new Guid("f2000328-5332-4247-7109-761847948afd"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Keman", null, null, "keman", "Approved" },
                    { new Guid("c323787c-2411-bb8c-45f2-19f616cabaec"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Türkçe (yabancılar için)", null, null, "turkce-yabancilar-icin", "Approved" },
                    { new Guid("c40bdd8f-66e1-acb6-83b4-4bb2128278e0"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Siber güvenlik", null, null, "siber-guvenlik", "Approved" },
                    { new Guid("c4d9f955-35d9-b3dc-43bb-7c339991d10c"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Fransızca", null, null, "fransizca", "Approved" },
                    { new Guid("c5264fa1-1be0-611e-80e3-f49722f9f1f6"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Muhasebe", null, null, "muhasebe", "Approved" },
                    { new Guid("c626b0e9-f688-23db-0986-a414850c1cd4"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kişisel finans", null, null, "kisisel-finans", "Approved" },
                    { new Guid("c8c6f883-1fe9-c061-3143-efcecffd92fd"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Yüzme", null, null, "yuzme", "Approved" },
                    { new Guid("ca6bba0a-ca31-fc10-33a6-992f167311dc"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Kimya", null, null, "kimya", "Approved" },
                    { new Guid("cfb4488a-927d-23f3-3071-4b465345eaf2"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Özgeçmiş ve mülakat hazırlığı", null, null, "ozgecmis-ve-mulakat-hazirligi", "Approved" },
                    { new Guid("d0cd419a-1763-e26d-51c8-f506d4ed12f6"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dijital pazarlama", null, null, "dijital-pazarlama", "Approved" },
                    { new Guid("d4c10516-0a5c-6e0f-cdb2-591a0a21f667"), new Guid("4b8e2d12-0fa5-338e-58e6-fa412b7f1400"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Proje yönetimi", null, null, "proje-yonetimi", "Approved" },
                    { new Guid("d89701b5-2506-da2f-918c-b07d9d1fb0f5"), new Guid("c4b12a74-832a-d4d2-847d-17d49896e76e"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Biyoloji", null, null, "biyoloji", "Approved" },
                    { new Guid("d9da18b4-2993-e942-25a8-9fb161308e23"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Yaratıcı yazarlık", null, null, "yaratici-yazarlik", "Approved" },
                    { new Guid("da713626-3cb8-ff27-ce78-8f8b0d9526e7"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "İtalyanca", null, null, "italyanca", "Approved" },
                    { new Guid("e59ddbc9-b010-e4f9-aec3-18605d43378b"), new Guid("1a057192-8a39-a4dd-a1d8-d1c89ed5bb85"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tenis", null, null, "tenis", "Approved" },
                    { new Guid("e8ae76cb-25f7-ada4-f035-e4b223e1f4b0"), new Guid("bb679251-7280-29ff-3e24-3af88f0e1341"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "İllüstrasyon", null, null, "illustrasyon", "Approved" },
                    { new Guid("eb571aa9-9f16-10da-2a98-adb6786e87a3"), new Guid("329aa7f0-8d9d-e5ca-5c48-48efc254cdff"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dikiş", null, null, "dikis", "Approved" },
                    { new Guid("eff0f211-6b95-b37f-4340-759a6269dafe"), new Guid("5e5be479-d32d-2447-93d2-db2d70c41870"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rusça", null, null, "rusca", "Approved" },
                    { new Guid("f32ef9da-73e3-7bb9-9555-378ba1191ca2"), new Guid("cd20e126-d483-acc8-0357-ffce2323d114"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Oyunculuk", null, null, "oyunculuk", "Approved" },
                    { new Guid("f63dfda2-9ddf-230f-7c36-990b0254f434"), new Guid("274753ff-0b72-03fa-bf9f-916d41c09ca6"), new DateTimeOffset(new DateTime(2026, 10, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Web geliştirme", null, null, "web-gelistirme", "Approved" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_skill_categories_slug",
                schema: "skills",
                table: "skill_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_category_id",
                schema: "skills",
                table: "skills",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_slug",
                schema: "skills",
                table: "skills",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skills_status_category_id",
                schema: "skills",
                table: "skills",
                columns: new[] { "status", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_skills_skill_id_type",
                schema: "skills",
                table: "user_skills",
                columns: new[] { "skill_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_user_skills_user_id_skill_id_type",
                schema: "skills",
                table: "user_skills",
                columns: new[] { "user_id", "skill_id", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_skills",
                schema: "skills");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "skills");

            migrationBuilder.DropTable(
                name: "skill_categories",
                schema: "skills");
        }
    }
}
