using System.IO;
using System.Linq;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class CategoriesServiceTests
    {
        private static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

        [Fact]
        public void BuildCategorySet_UsesKeywords_WhenNoExplicitSymptoms()
        {
            var svc = new CategoriesService(DataPath("categories.min.json"));
            var fakeVocab = new[] { "Cough", "Runny Nose", "Sore Throat", "Chest Pain" };
            var cat = svc.GetAllCategories().First(c => c.Name == "Respiratory");
            var set = svc.BuildCategorySet(cat, fakeVocab);
            Assert.Contains("Cough", set);
            Assert.Contains("Sore Throat", set);
            Assert.DoesNotContain("Chest Pain", set);
        }
    }
}
