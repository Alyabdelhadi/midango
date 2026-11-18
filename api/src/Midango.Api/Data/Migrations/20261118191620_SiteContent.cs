using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Midango.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SiteContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_faqs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    question_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    answer_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    answer_ar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_faqs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    contact_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    whats_app = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    instagram = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_settings", x => x.id);
                    table.CheckConstraint("ck_site_settings_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "site_texts",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    en = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ar = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_texts", x => x.key);
                });

            migrationBuilder.InsertData(
                table: "site_faqs",
                columns: new[] { "id", "answer_ar", "answer_en", "created_at", "question_ar", "question_en", "sort_order" },
                values: new object[,]
                {
                    { new Guid("019a9700-0000-7000-8000-000000000001"), "نعم. تنزيل التطبيق والحجز من خلاله مجانيان، وتدفع فقط سعر الملعب.", "Yes. Downloading the app and booking are free. You only pay for the court.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "هل التطبيق مجاني للاعبين؟", "Is the app free for players?", 1 },
                    { new Guid("019a9700-0000-7000-8000-000000000002"), "نقدًا في الملعب. أظهر رمز QR الخاص بحجزك في الاستقبال وادفع عند وصولك.", "In cash at the venue. Show your booking's QR code at the desk and pay when you arrive.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "كيف أدفع؟", "How do I pay?", 2 },
                    { new Guid("019a9700-0000-7000-8000-000000000003"), "نعم، مجانًا حتى تبدأ مهلة الإلغاء التي يحددها الملعب.", "Yes, free of charge until the venue's cancellation window starts.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "هل يمكنني إلغاء الحجز؟", "Can I cancel a booking?", 3 },
                    { new Guid("019a9700-0000-7000-8000-000000000004"), "تواصل معنا وسنجهّز منشأتك. أرسل طلب عرض تجريبي وسنتصل بك.", "Talk to us and we'll set your venue up. Send a demo request and we'll call you.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "كم تكلّف MIDANGO أصحاب الملاعب؟", "What does MIDANGO cost venues?", 4 },
                    { new Guid("019a9700-0000-7000-8000-000000000005"), "البادل وكرة القدم وغيرها، حسب ما يقدّمه كل ملعب.", "Padel, football and more, depending on what each venue offers.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ما الرياضات التي يمكنني حجزها؟", "Which sports can I book?", 5 },
                    { new Guid("019a9700-0000-7000-8000-000000000006"), "نعم. التطبيق ولوحة التحكم ثنائيا اللغة بالكامل، بالعربية والإنجليزية.", "Yes. The app and the dashboard are fully bilingual, in English and Arabic.", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "هل يدعم اللغة العربية؟", "Is it available in Arabic?", 6 }
                });

            migrationBuilder.InsertData(
                table: "site_settings",
                columns: new[] { "id", "contact_email", "contact_phone", "instagram", "updated_at", "whats_app" },
                values: new object[] { 1, null, null, null, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.InsertData(
                table: "site_texts",
                columns: new[] { "key", "ar", "en", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { "faq.lede", "عندك سؤال آخر؟ اكتبه في نموذج طلب العرض.", "Something else? Ask us in the demo form.", 20, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "faq.title", "أسئلة وأجوبة.", "Questions, answered.", 19, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "footer.tagline", "حجز الملاعب في الخليج.", "Court booking for the Gulf.", 21, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "hero.lede", "MIDANGO منصة لحجز الملاعب في الخليج. يجد اللاعبون ملعبًا ويحجزونه في ثوانٍ، وتدير الملاعب الجدول والاستقبال والنقد من لوحة تحكم واحدة.", "MIDANGO is court booking for the Gulf. Players find a court and book it in seconds. Venues run the schedule, the desk and the cash from one dashboard.", 2, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "hero.title", "مباراتك القادمة على بُعد ضغطة.", "Press play on your next game.", 1, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.kicker", "للاعبين", "For players", 3, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.lede", "اعثر على ملعب قريب منك، وشاهد الأوقات المتاحة واحجز. لا تحتاج إلى بطاقة: الدفع في الملعب.", "Find a venue near you, see what's free and book it. No card needed: you pay at the venue.", 5, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step1.body", "بادل وكرة قدم وغيرها. تصفّح الملاعب القريبة منك مع ملاعبها ومواعيدها وأسعارها.", "Padel, football and more. Browse venues near you with their courts, opening hours and prices.", 7, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step1.title", "اختر رياضتك", "Pick your sport", 6, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step2.body", "شاهد الأوقات المتاحة كما هي الآن واحجز ما يناسبك، ويتأكد حجزك فورًا.", "See open slots as they are and book the one that suits you. Your booking is confirmed straight away.", 9, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step2.title", "اختر الملعب والوقت", "Pick a court and time", 8, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step3.body", "كل حجز يأتي برمز QR. أظهره عند وصولك وادفع نقدًا في الملعب.", "Every booking comes with a QR code. Show it when you arrive and pay cash at the venue.", 11, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.step3.title", "أظهر رمز QR في الاستقبال", "Show your QR at the desk", 10, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "players.title", "من بيتك إلى الملعب في ثلاث خطوات.", "From your sofa to the court in three steps.", 4, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "request.kicker", "اطلب عرضًا تجريبيًا", "Request a demo", 16, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "request.lede", "أخبرنا عن منشأتك وسنتصل بك لتجهيزها. بدون أي التزام.", "Tell us about your venue and we'll call you to set it up. No commitment.", 18, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "request.title", "انضم بمنشأتك إلى MIDANGO.", "Bring your venue to MIDANGO.", 17, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "stores.note", "يصدر التطبيق قريبًا بالعربية والإنجليزية.", "The app launches soon, in English and Arabic.", 12, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "venues.kicker", "لأصحاب الملاعب", "For venues", 13, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "venues.lede", "MIDANGO Business لوحة تحكم على الويب لموظفي الاستقبال والمديرين، بالعربية والإنجليزية.", "MIDANGO Business is the web dashboard for your front desk and your managers, in English and Arabic.", 15, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "venues.title", "منشأتك كلها على شاشة واحدة.", "Your whole venue on one screen.", 14, new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_faqs_sort_order",
                table: "site_faqs",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_faqs");

            migrationBuilder.DropTable(
                name: "site_settings");

            migrationBuilder.DropTable(
                name: "site_texts");
        }
    }
}
