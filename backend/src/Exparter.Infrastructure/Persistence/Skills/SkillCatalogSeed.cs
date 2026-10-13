using System.Security.Cryptography;
using System.Text;
using Exparter.Domain.Skills;

namespace Exparter.Infrastructure.Persistence.Skills;

/// <summary>
/// The catalog every installation starts with. Changing this list changes the model, so
/// each change needs a migration; skills proposed by users are not part of it.
/// </summary>
internal static class SkillCatalogSeed
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    private static readonly (string Category, string[] Skills)[] Catalog =
    [
        ("Diller",
        [
            "İngilizce", "Almanca", "Fransızca", "İspanyolca", "İtalyanca", "Rusça", "Arapça", "Japonca", "Korece",
            "Türkçe (yabancılar için)", "İşaret dili",
        ]),
        ("Müzik", ["Gitar", "Piyano", "Keman", "Bağlama", "Bateri", "Şan", "Ney", "Müzik prodüksiyonu"]),
        ("Yazılım ve teknoloji",
        [
            "Python", "JavaScript", "Web geliştirme", "Mobil uygulama geliştirme", "Veri analizi",
            "Yapay zekâ ve makine öğrenmesi", "Siber güvenlik", "Excel",
        ]),
        ("Tasarım ve görsel sanatlar",
        [
            "Grafik tasarım", "UI/UX tasarımı", "Fotoğrafçılık", "Video düzenleme", "Resim", "İllüstrasyon", "3D modelleme",
        ]),
        ("Spor ve hareket", ["Yoga", "Pilates", "Yüzme", "Tenis", "Koşu", "Fitness", "Dans"]),
        ("Mutfak", ["Türk mutfağı", "Pastacılık", "Ekmek yapımı", "Dünya mutfakları", "Kahve demleme", "Vegan mutfak"]),
        ("El işleri ve zanaat", ["Örgü", "Dikiş", "Seramik", "Ahşap işleri", "Takı tasarımı", "Ebru"]),
        ("Akademik", ["Matematik", "Fizik", "Kimya", "Biyoloji", "Tarih", "Edebiyat"]),
        ("İş ve kariyer",
        [
            "Topluluk önünde konuşma", "Dijital pazarlama", "Girişimcilik", "Proje yönetimi", "Muhasebe",
            "Özgeçmiş ve mülakat hazırlığı",
        ]),
        ("Yaşam ve hobi", ["Satranç", "Bahçecilik", "Oyunculuk", "Yaratıcı yazarlık", "Meditasyon", "Kişisel finans"]),
    ];

    public static IEnumerable<object> Categories() =>
        Catalog.Select((entry, index) => new
        {
            Id = IdFor("category", entry.Category),
            Name = entry.Category,
            Slug = Slug.From(entry.Category),
            SortOrder = index + 1,
        });

    public static IEnumerable<object> Skills() =>
        Catalog.SelectMany(entry => entry.Skills.Select(name => new
        {
            Id = IdFor("skill", name),
            CategoryId = IdFor("category", entry.Category),
            Name = name,
            Slug = Slug.From(name),
            Status = SkillStatus.Approved,
            ProposedByUserId = (Guid?)null,
            CreatedAt = SeededAt,
            ReviewedAt = (DateTimeOffset?)null,
        }));

    /// <summary>
    /// Seeded rows need ids that are the same on every machine and in every migration;
    /// deriving them from the name keeps them stable without a hand-maintained table.
    /// </summary>
    private static Guid IdFor(string kind, string name) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{kind}:{Slug.From(name)}")));
}
