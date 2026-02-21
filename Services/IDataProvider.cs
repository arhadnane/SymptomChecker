using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Abstraction for loading / saving application data.
    /// Enables swapping JSON files for database, API, or in-memory test sources.
    /// </summary>
    public interface IDataProvider
    {
        ConditionDatabase LoadConditions();
        void SaveConditions(ConditionDatabase db);
        SymptomCategoryDatabase LoadCategories();
        SynonymDatabase LoadSynonyms();
    }
}
