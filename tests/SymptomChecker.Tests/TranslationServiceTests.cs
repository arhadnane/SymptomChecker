using System.IO;
using SymptomChecker.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class TranslationServiceTests
    {
        private static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

        [Fact]
        public void T_ReturnsLocalizedOrFallback()
        {
            var t = new TranslationService(DataPath("translations.min.json"));
            t.SetLanguage("fr");
            Assert.Equal("Aide", t.T("Help"));
            t.SetLanguage("ar");
            Assert.Equal("مساعدة", t.T("Help"));
            t.SetLanguage("en");
            Assert.Equal("Help", t.T("Help"));
        }

        [Fact]
        public void Symptom_Condition_Category_Localize()
        {
            var t = new TranslationService(DataPath("translations.min.json"));
            t.SetLanguage("fr");
            Assert.Equal("Toux", t.Symptom("Cough"));
            Assert.Equal("Pneumonie", t.Condition("Pneumonia"));
            Assert.Equal("Respiratoire", t.Category("Respiratory"));
        }
    }
}
