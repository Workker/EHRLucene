using System.Collections.ObjectModel;
using EHRIntegracao.Domain.Factorys;
using EHRIntegracao.Domain.Services.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace EHRLucene.Domain
{
    public class LuceneClient
    {

        public LuceneClient(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine("E://Projects//EHR//lucene_index");
        }

        public void CriarDiretorio()
        {
            if (!System.IO.Directory.Exists(_luceneDir)) System.IO.Directory.CreateDirectory(_luceneDir);
        }

        public string _luceneDir;
        private FSDirectory _directoryTemp;
        private FSDirectory _directory
        {
            get
            {
                if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
                if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
                var lockFilePath = Path.Combine(_luceneDir, "write.lock");
                //if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
                return _directoryTemp;
            }
        }

        public void AddUpdateLuceneIndex(PatientDTO patients)
        {
            AddUpdateLuceneIndex(new List<PatientDTO> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<IPatientDTO> sampleDatas)
        {
            // init lucene
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                // add data to lucene search index (replaces older entries if any)
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                // close handles
                analyzer.Close();
                writer.Close();
                writer.Dispose();
            }
        }

        private void _addToLuceneIndex(IPatientDTO patient, IndexWriter writer)
        {
            // remove older index entry
            var searchQuery = new TermQuery(new Term("Id", patient.Id.ToString()));
            writer.DeleteDocuments(searchQuery);

            var doc = new Document();

            doc.Add(new Field("Id", patient.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Name", patient.Name, Field.Store.YES, Field.Index.ANALYZED));

            if (!string.IsNullOrEmpty(patient.CPF))
            {
                doc.Add(new Field("CPF", patient.CPF, Field.Store.YES, Field.Index.ANALYZED));
            }

            doc.Add(new Field("DateBirthday", patient.DateBirthday, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Hospital", patient.Hospital.ToString(), Field.Store.YES, Field.Index.ANALYZED));

            // add entry to index
            writer.AddDocument(doc);
        }

        public IEnumerable<IPatientDTO> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<IPatientDTO>() : _SimpleSearch(input);

        }

        public IEnumerable<IPatientDTO> AdvancedSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<IPatientDTO>() : _AdvancedSearch(input);
        }

        private IEnumerable<IPatientDTO> _AdvancedSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "DateBirthday"}, analyzer);
                parser.DefaultOperator= QueryParser.Operator.AND;
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Close();
                searcher.Dispose();

                return results;
            }
        }

        private IEnumerable<IPatientDTO> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Name", "CPF"}, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Close();
                searcher.Dispose();

                return results;

            }
        }

        private string _removeSpecialCharacters(string searchQuery)
        {
            return searchQuery.Replace("*", "").Replace("?", "");
        }

        private bool _inputIsNotNullOrEmpty(string input)
        {
            return string.IsNullOrEmpty(input);
        }

        private Query parseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery.Trim());
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        private IEnumerable<IPatientDTO> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private IPatientDTO _mapLuceneDocumentToData(Document doc)
        {
            DbEnum valor;
            var enumHospital = Enum.TryParse(doc.Get("Hospital"), true, out valor);

            return new PatientDTO()
            {
                Id = doc.Get("Id"),
                Name = doc.Get("Name"),
                Hospital = enumHospital ? valor : DbEnum.sumario,
                DateBirthday = "DateBirthday"
            };
        }

        private void Optimize()
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
                writer.Close();
                writer.Dispose();
            }
        }

    }
}
