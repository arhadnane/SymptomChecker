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

        [Fact]
        public void RuntimeTranslations_ContainGuidedWizardAndProfessionalSectionKeys()
        {
            var runtimePath = Path.Combine(AppContext.BaseDirectory, "data", "translations.json");
            var t = new TranslationService(runtimePath);

            t.SetLanguage("en");
            Assert.NotEqual("Patient_Step1_Title", t.T("Patient_Step1_Title"));
            Assert.NotEqual("Patient_RunCheck", t.T("Patient_RunCheck"));

            t.SetLanguage("fr");
            Assert.NotEqual("Patient_Step4_Title", t.T("Patient_Step4_Title"));

            t.SetLanguage("ar");
            Assert.NotEqual("Pro_Section_Ai", t.T("Pro_Section_Ai"));
            Assert.NotEqual("Pro_Section_Symptoms", t.T("Pro_Section_Symptoms"));
        }
    }
}
