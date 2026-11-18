namespace Midango.Api.Platform;

/// One editable text on the public website, by fixed key (e.g. "hero.title"). The set of keys is seeded
/// by the migration; the operator edits the wording, never adds or removes keys.
public sealed class SiteText
{
    public required string Key { get; init; }
    public required string En { get; set; }
    public required string Ar { get; set; }
    public int SortOrder { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// One question/answer pair of the website FAQ, shown by SortOrder.
public sealed class SiteFaq
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string QuestionEn { get; set; }
    public required string QuestionAr { get; set; }
    public required string AnswerEn { get; set; }
    public required string AnswerAr { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// The website's contact details. Exactly one row (Id = 1), seeded by the migration. Store links live on AppContent.
public sealed class SiteSettings
{
    public const int SingletonId = 1;
    public int Id { get; init; } = SingletonId;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Instagram { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// Today's website wording (website/index.html and ar/index.html), seeded by the SiteContent migration.
internal static class SiteSeed
{
    public static readonly DateTimeOffset At = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    private static SiteText T(int order, string key, string en, string ar) =>
        new() { Key = key, En = en, Ar = ar, SortOrder = order, UpdatedAt = At };

    public static readonly SiteText[] Texts =
    [
        T(1, "hero.title", "Press play on your next game.", "مباراتك القادمة على بُعد ضغطة."),
        T(2, "hero.lede",
            "MIDANGO is court booking for the Gulf. Players find a court and book it in seconds. Venues run the schedule, the desk and the cash from one dashboard.",
            "MIDANGO منصة لحجز الملاعب في الخليج. يجد اللاعبون ملعبًا ويحجزونه في ثوانٍ، وتدير الملاعب الجدول والاستقبال والنقد من لوحة تحكم واحدة."),
        T(3, "players.kicker", "For players", "للاعبين"),
        T(4, "players.title", "From your sofa to the court in three steps.", "من بيتك إلى الملعب في ثلاث خطوات."),
        T(5, "players.lede",
            "Find a venue near you, see what's free and book it. No card needed: you pay at the venue.",
            "اعثر على ملعب قريب منك، وشاهد الأوقات المتاحة واحجز. لا تحتاج إلى بطاقة: الدفع في الملعب."),
        T(6, "players.step1.title", "Pick your sport", "اختر رياضتك"),
        T(7, "players.step1.body",
            "Padel, football and more. Browse venues near you with their courts, opening hours and prices.",
            "بادل وكرة قدم وغيرها. تصفّح الملاعب القريبة منك مع ملاعبها ومواعيدها وأسعارها."),
        T(8, "players.step2.title", "Pick a court and time", "اختر الملعب والوقت"),
        T(9, "players.step2.body",
            "See open slots as they are and book the one that suits you. Your booking is confirmed straight away.",
            "شاهد الأوقات المتاحة كما هي الآن واحجز ما يناسبك، ويتأكد حجزك فورًا."),
        T(10, "players.step3.title", "Show your QR at the desk", "أظهر رمز QR في الاستقبال"),
        T(11, "players.step3.body",
            "Every booking comes with a QR code. Show it when you arrive and pay cash at the venue.",
            "كل حجز يأتي برمز QR. أظهره عند وصولك وادفع نقدًا في الملعب."),
        T(12, "stores.note", "The app launches soon, in English and Arabic.", "يصدر التطبيق قريبًا بالعربية والإنجليزية."),
        T(13, "venues.kicker", "For venues", "لأصحاب الملاعب"),
        T(14, "venues.title", "Your whole venue on one screen.", "منشأتك كلها على شاشة واحدة."),
        T(15, "venues.lede",
            "MIDANGO Business is the web dashboard for your front desk and your managers, in English and Arabic.",
            "MIDANGO Business لوحة تحكم على الويب لموظفي الاستقبال والمديرين، بالعربية والإنجليزية."),
        T(16, "request.kicker", "Request a demo", "اطلب عرضًا تجريبيًا"),
        T(17, "request.title", "Bring your venue to MIDANGO.", "انضم بمنشأتك إلى MIDANGO."),
        T(18, "request.lede",
            "Tell us about your venue and we'll call you to set it up. No commitment.",
            "أخبرنا عن منشأتك وسنتصل بك لتجهيزها. بدون أي التزام."),
        T(19, "faq.title", "Questions, answered.", "أسئلة وأجوبة."),
        T(20, "faq.lede", "Something else? Ask us in the demo form.", "عندك سؤال آخر؟ اكتبه في نموذج طلب العرض."),
        T(21, "footer.tagline", "Court booking for the Gulf.", "حجز الملاعب في الخليج."),
    ];

    private static SiteFaq F(int order, string id, string qEn, string aEn, string qAr, string aAr) =>
        new()
        {
            Id = Guid.Parse(id), QuestionEn = qEn, AnswerEn = aEn, QuestionAr = qAr, AnswerAr = aAr,
            SortOrder = order, CreatedAt = At,
        };

    public static readonly SiteFaq[] Faqs =
    [
        F(1, "019a9700-0000-7000-8000-000000000001",
            "Is the app free for players?", "Yes. Downloading the app and booking are free. You only pay for the court.",
            "هل التطبيق مجاني للاعبين؟", "نعم. تنزيل التطبيق والحجز من خلاله مجانيان، وتدفع فقط سعر الملعب."),
        F(2, "019a9700-0000-7000-8000-000000000002",
            "How do I pay?", "In cash at the venue. Show your booking's QR code at the desk and pay when you arrive.",
            "كيف أدفع؟", "نقدًا في الملعب. أظهر رمز QR الخاص بحجزك في الاستقبال وادفع عند وصولك."),
        F(3, "019a9700-0000-7000-8000-000000000003",
            "Can I cancel a booking?", "Yes, free of charge until the venue's cancellation window starts.",
            "هل يمكنني إلغاء الحجز؟", "نعم، مجانًا حتى تبدأ مهلة الإلغاء التي يحددها الملعب."),
        F(4, "019a9700-0000-7000-8000-000000000004",
            "What does MIDANGO cost venues?", "Talk to us and we'll set your venue up. Send a demo request and we'll call you.",
            "كم تكلّف MIDANGO أصحاب الملاعب؟", "تواصل معنا وسنجهّز منشأتك. أرسل طلب عرض تجريبي وسنتصل بك."),
        F(5, "019a9700-0000-7000-8000-000000000005",
            "Which sports can I book?", "Padel, football and more, depending on what each venue offers.",
            "ما الرياضات التي يمكنني حجزها؟", "البادل وكرة القدم وغيرها، حسب ما يقدّمه كل ملعب."),
        F(6, "019a9700-0000-7000-8000-000000000006",
            "Is it available in Arabic?", "Yes. The app and the dashboard are fully bilingual, in English and Arabic.",
            "هل يدعم اللغة العربية؟", "نعم. التطبيق ولوحة التحكم ثنائيا اللغة بالكامل، بالعربية والإنجليزية."),
    ];
}
