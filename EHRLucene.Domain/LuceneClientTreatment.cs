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


    public class LuceneClientTreatment
    {

        public LuceneClientTreatment(string path)
        {
            InformarPath(path);
            CriarDiretorio();

        }

        private void InformarPath(string path)
        {
            _luceneDir = Path.Combine(HttpContext.Current.Request.PhysicalApplicationPath, "lucene_index_Treatment");
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

        public void AddUpdateLuceneIndex(ITreatmentDTO patients)
        {
            AddUpdateLuceneIndex(new List<ITreatmentDTO> { patients });
            Optimize();
        }

        public void AddUpdateLuceneIndex(IEnumerable<ITreatmentDTO> sampleDatas)
        {
            // init lucene
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                // add data to lucene search index (replaces older entries if any)
                foreach (var sampleData in sampleDatas) _addToLuceneIndex(sampleData, writer);

                // close handles
                analyzer.Close();
                writer.Dispose();
            }
        }

        private void _addToLuceneIndex(ITreatmentDTO treatment, IndexWriter writer)
        {
            // RemoveIndex(treatment, writer);
            var doc = new Document();
            AddFields(treatment, doc);
            writer.AddDocument(doc);
        }

        private void AddFields(ITreatmentDTO patient, Document doc)
        {
            doc.Add(new Field("Id", patient.Id.ToString(), Field.Store.YES, Field.Index.ANALYZED));
        }

        private void RemoveIndex(ITreatmentDTO patient, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", patient.Id.ToString()));
            writer.DeleteDocuments(searchQuery);
        }

        public IEnumerable<ITreatmentDTO> SimpleSearch(string input)
        {
            return _inputIsNotNullOrEmpty(input) ? new List<ITreatmentDTO>() : _SimpleSearch(input);

        }

        public IEnumerable<ITreatmentDTO> AdvancedSearch(List<string> medicalRecords)
        {
            return _AdvancedSearch(medicalRecords);
        }

        private string TreatCharacters(List<string> medicalRecords)
        {
            var str = "";

            var i = 1;
            foreach (var h in medicalRecords)
            {
                if (medicalRecords.Count > 1 && i < medicalRecords.Count)
                {
                    str += " Id:" + h + " OR ";
                }
                else
                {
                    str += " Id:" + h;
                }
                i++;
            }

            return str;
        }

        private IEnumerable<ITreatmentDTO> _AdvancedSearch(List<string> medicalRecords)
        {
            var searchQueryStr = TreatCharacters(medicalRecords);

            using (var searcher = new IndexSearcher(_directory, false))
            {

                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                string[] array = CreatParameters(medicalRecords);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, array, analyzer);

                parser.DefaultOperator = QueryParser.Operator.AND;


                var query = parseQuery(searchQueryStr, parser);
                var hits = searcher.Search(query, null, 5000000, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
                searcher.Dispose();

                return results;
            }
        }

        private string[] CreatParameters(List<string> hospital)
        {
            var parameters = new List<string>();
            parameters.Add("Id");
            return parameters.ToArray();
        }



        private MultiFieldQueryParser CreateParser(StandardAnalyzer analyzer)
        {
            var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
            parser.DefaultOperator = QueryParser.Operator.AND;
            return parser;
        }


        private IEnumerable<ITreatmentDTO> _SimpleSearch(string searchQuery)
        {
            searchQuery = _removeSpecialCharacters(searchQuery);

            using (var searcher = new IndexSearcher(_directory, false))
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                var parser = new MultiFieldQueryParser(Version.LUCENE_30, new[] { "Id" }, analyzer);
                var query = parseQuery(searchQuery, parser);
                var hits = searcher.Search(query, null, 10, Sort.RELEVANCE).ScoreDocs;
                var results = _mapLuceneToDataList(hits, searcher);

                analyzer.Close();
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
                query = parser.Parse(searchQuery);
            }
            catch (ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        private IEnumerable<ITreatmentDTO> _mapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher)
        {
            return hits.Select(hit => _mapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private ITreatmentDTO _mapLuceneDocumentToData(Document doc)
        {

            var treatment = new TreatmentDTO()
            {
                Id = doc.Get("Id"),
            };

            return treatment;
        }

        private void Optimize()
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
                writer.Dispose();
            }
        }

    }
}
