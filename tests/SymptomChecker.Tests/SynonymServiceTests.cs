using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class SynonymServiceTests
    {
        private static string DataPath(string file) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", file);

        private readonly SynonymService _service;
        private readonly List<string> _vocabulary = new()
        {
            "Cough", "Runny Nose", "Fever", "Shortness of Breath",
            "Sore Throat", "Chest Pain", "Headache"
        };

        public SynonymServiceTests()
        {
            _service = new SynonymService(DataPath("synonyms.min.json"));
        }

        [Fact]
        public void BuildAliasToCanonical_MapsAliasesToCanonical()
        {
            var map = _service.BuildAliasToCanonical(_vocabulary);

            Assert.Equal("Cough", map["Tussis"]);
            Assert.Equal("Runny Nose", map["Rhinorrhea"]);
            Assert.Equal("Fever", map["Pyrexia"]);
            Assert.Equal("Fever", map["High Temperature"]);
        }

        [Fact]
        public void BuildAliasToCanonical_CanonicalMapsToItself()
        {
            var map = _service.BuildAliasToCanonical(_vocabulary);

            Assert.Equal("Cough", map["Cough"]);
            Assert.Equal("Fever", map["Fever"]);
        }

        [Fact]
        public void BuildAliasToCanonical_CaseInsensitive()
        {
            var map = _service.BuildAliasToCanonical(_vocabulary);

            Assert.Equal("Cough", map["tussis"]);
            Assert.Equal("Cough", map["TUSSIS"]);
        }

        [Fact]
        public void MatchSymptomsByQuery_AliasTerm_ReturnsCanonical()
        {
            var results = _service.MatchSymptomsByQuery("Tussis", _vocabulary).ToList();

            Assert.Contains("Cough", results);
        }

        [Fact]
        public void MatchSymptomsByQuery_CanonicalTerm_ReturnsItself()
        {
            var results = _service.MatchSymptomsByQuery("Cough", _vocabulary).ToList();

            Assert.Contains("Cough", results);
        }

        [Fact]
        public void MatchSymptomsByQuery_PartialAlias_MatchesViaSubstring()
        {
            // "Rhinor" is a partial match for "Rhinorrhea" alias
            var results = _service.MatchSymptomsByQuery("Rhinor", _vocabulary).ToList();

            Assert.Contains("Runny Nose", results);
        }

        [Fact]
        public void MatchSymptomsByQuery_EmptyQuery_ReturnsAll()
        {
            var results = _service.MatchSymptomsByQuery("", _vocabulary).ToList();

            Assert.Equal(_vocabulary.Count, results.Count);
        }

        [Fact]
        public void MatchSymptomsByQuery_NoMatch_FallsBackToSubstring()
        {
            // "Head" doesn't match any synonym but is a substring of "Headache"
            var results = _service.MatchSymptomsByQuery("Head", _vocabulary).ToList();

            Assert.Contains("Headache", results);
        }

        [Fact]
        public void MatchSymptomsByQuery_CaseInsensitive()
        {
            var results = _service.MatchSymptomsByQuery("dyspnea", _vocabulary).ToList();

            Assert.Contains("Shortness of Breath", results);
        }
    }
}
